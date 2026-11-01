using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Audit;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Sessions;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>A clock the tests move (grace boundary, expiry, session-check cache window).</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset? _now;
    public override DateTimeOffset GetUtcNow() => _now ?? DateTimeOffset.UtcNow;
    public void Set(DateTimeOffset now) => _now = now;
    public void Advance(TimeSpan by) => _now = GetUtcNow() + by;
    public void Reset() => _now = null;
}

/// <summary>Throwaway SQL Server host with a controllable clock.</summary>
public class SessionApiFactory : SqlServerApiFactory
{
    public TestClock Clock { get; } = new();

    protected override string DatabasePrefix => "WazzufJobs_SessionTest_";

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
    }
}

/// <summary>The same with READ_COMMITTED_SNAPSHOT on: rotation must stay exactly-once under row versioning too.</summary>
public sealed class RcsiSessionApiFactory : SessionApiFactory
{
    protected override string DatabasePrefix => "WazzufJobs_SessionRcsiTest_";

    protected override void PrepareDatabase(ApplicationDBContext context)
    {
        base.PrepareDatabase(context);
        context.Database.ExecuteSqlRaw("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE");
    }
}

/// <summary>Shared helpers for the session tests.</summary>
public abstract class SessionTestBase(SessionApiFactory factory)
{
    protected const string Password = "Session#Passw0rd!6a";
    protected SessionApiFactory Factory { get; } = factory;

    protected async Task<AppUser> CreateUserAsync(AccountType accountType = AccountType.Candidate, string role = PlatformRoles.Candidate)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"session-{Guid.NewGuid():N}@example.test";
        var user = new AppUser { UserName = email, Email = email, FirstName = "Session", LastName = "Test", EmailConfirmed = true, AccountType = accountType };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    /// <summary>Signs in over HTTP (unchanged legacy contract) and returns the access token and its session.</summary>
    protected async Task<(string AccessToken, Guid SessionId, JsonElement Body)> LoginAsync(AppUser user, string? password = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WazzufJobs-SessionTests/1.0");
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = password ?? Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var token = body.GetProperty("token").GetString()!;
        return (token, Guid.Parse(Claim(token, "sid")!), body);
    }

    protected static string? Claim(string token, string type) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.FirstOrDefault(c => c.Type == type)?.Value;

    protected HttpClient Bearer(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<HttpStatusCode> ProbeAsync(string accessToken) => (await Bearer(accessToken).GetAsync("/api/v1/me/sessions")).StatusCode;

    protected async Task<T> WithSessionsAsync<T>(Func<ISessionService, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISessionService>());
    }

    protected Task<Result<IssuedRefreshToken>> IssueAsync(Guid sessionId) => WithSessionsAsync(s => s.IssueRefreshTokenAsync(sessionId, CancellationToken.None));
    protected Task<Result<RotatedSession>> RotateAsync(string token) => WithSessionsAsync(s => s.RotateAsync(token, CancellationToken.None));

    protected async Task<T> DbAsync<T>(Func<ApplicationDBContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDBContext>());
    }

    protected Task<int> UsableTokensAsync(Guid sessionId) =>
        DbAsync(db => db.SessionRefreshTokens.CountAsync(t => t.SessionId == sessionId && t.ConsumedAt == null && t.RevokedAt == null));

    protected Task<UserSession> SessionAsync(Guid sessionId) => DbAsync(db => db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId));

    protected async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    protected static async Task<T[]> Concurrently<T>(int count, Func<Task<T>> action)
    {
        using var start = new SemaphoreSlim(0, count);
        var tasks = Enumerable.Range(0, count).Select(async _ => { await start.WaitAsync(); return await action(); }).ToArray();
        start.Release(count);
        return await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Rotation under real concurrency (Phase 02, S6a; P02-4): 20 parallel rotations of one token give exactly one
/// successor and nineteen 409 races without credentials, and one usable token remains. Run with RCSI off and on.
/// </summary>
public abstract class RotationConcurrencyTestsBase(SessionApiFactory factory) : SessionTestBase(factory)
{
    [SqlServerFact]
    public async Task Twenty_parallel_rotations_of_one_token_have_exactly_one_winner()
    {
        Factory.Clock.Reset();
        for (var round = 0; round < 3; round++)
        {
            var (_, sessionId, _) = await LoginAsync(await CreateUserAsync());
            var token = (await IssueAsync(sessionId)).Value.Token;

            var results = await Concurrently(20, () => RotateAsync(token));

            var winner = Assert.Single(results, r => r.IsSuccess);
            Assert.All(results.Where(r => !r.IsSuccess), r => Assert.Equal("Auth.RefreshRaced", r.Error.Code));
            Assert.Equal(19, results.Count(r => !r.IsSuccess));
            Assert.Equal(1, await UsableTokensAsync(sessionId));
            Assert.Equal(2, await DbAsync(db => db.SessionRefreshTokens.CountAsync(t => t.SessionId == sessionId)));   // nothing issued to losers
            Assert.Equal(HttpStatusCode.OK, await ProbeAsync(winner.Value.AccessToken));
            Assert.True((await RotateAsync(winner.Value.RefreshToken)).IsSuccess);
        }
    }
}

public sealed class RotationConcurrencyTests(SessionApiFactory factory)
    : RotationConcurrencyTestsBase(factory), IClassFixture<SessionApiFactory>;

public sealed class RotationConcurrencyRcsiTests(RcsiSessionApiFactory factory)
    : RotationConcurrencyTestsBase(factory), IClassFixture<RcsiSessionApiFactory>
{
    [SqlServerFact]
    public async Task The_database_really_runs_with_read_committed_snapshot() =>
        Assert.True(await DbAsync(db => db.Database.SqlQuery<bool>($"SELECT is_read_committed_snapshot_on AS Value FROM sys.databases WHERE name = DB_NAME()").SingleAsync()));
}

/// <summary>
/// Sessions, refresh tokens, the session check and revocation (Phase 02, S6a; P02-4, P02-5): hashed storage, grace and
/// replay, attacker-first ordering, expiry, failure rollback, revocation hooks, the session API and login concurrency.
/// </summary>
public sealed class SessionTests(SessionApiFactory factory) : SessionTestBase(factory), IClassFixture<SessionApiFactory>
{
    // ---------------------------------------------------------------- sign-in creates a session; access tokens

    [SqlServerFact]
    public async Task Sign_in_keeps_its_contract_and_creates_a_session_with_a_15_minute_access_token()
    {
        Factory.Clock.Reset();
        var user = await CreateUserAsync();

        var (token, sessionId, body) = await LoginAsync(user);

        Assert.Equal(
            new[] { "id", "email", "userName", "firstName", "lastName", "token", "expiresIn", "refreshToken", "refreshTokenExpiration" }.Order(),
            body.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(900, body.GetProperty("expiresIn").GetInt32());   // Jwt:ExpiryMinutes is 30 in this host: capped
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalSeconds, 840, 901);   // exp is at most 15 minutes away
        Assert.Equal("pwd", Claim(token, "amr"));

        var session = await SessionAsync(sessionId);
        Assert.Equal(user.Id, session.UserId);
        Assert.Null(session.RevokedAt);
        Assert.Equal("WazzufJobs-SessionTests/1.0", session.UserAgent);
        Assert.InRange(session.AbsoluteExpiresAt - session.CreatedAt, TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1), TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(token));
    }

    [SqlServerFact]
    public async Task Staff_sessions_end_after_24_hours_and_their_tokens_slide_8_hours()
    {
        Factory.Clock.Reset();
        var (_, sessionId, _) = await LoginAsync(await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin));

        var session = await SessionAsync(sessionId);
        var issued = (await IssueAsync(sessionId)).Value;

        Assert.InRange(session.AbsoluteExpiresAt - session.CreatedAt, TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1), TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
        Assert.InRange(issued.ExpiresAt - session.CreatedAt, TimeSpan.FromHours(8) - TimeSpan.FromSeconds(1), TimeSpan.FromHours(8) + TimeSpan.FromSeconds(1));
    }

    [SqlServerFact]
    public async Task Tokens_without_a_valid_session_of_their_own_user_are_refused()
    {
        Factory.Clock.Reset();
        var user = await CreateUserAsync();
        var other = await CreateUserAsync();
        var (_, otherSession, _) = await LoginAsync(other);
        var jwt = Factory.Services.GetRequiredService<IJwtProvider>();
        var grant = PermissionCatalog.Resolve(AccountType.Candidate, false, [PlatformRoles.Candidate]);

        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(jwt.GenerateToken(user, grant, Guid.NewGuid(), "pwd").token));   // unknown session
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(jwt.GenerateToken(user, grant, otherSession, "pwd").token));    // another user's session
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(TokenWithoutSession(user)));                                    // no sid at all
    }

    // ---------------------------------------------------------------- refresh tokens: storage, issue, rotation

    [SqlServerFact]
    public async Task Refresh_tokens_are_random_and_stored_only_as_hashes_with_one_usable_per_session()
    {
        Factory.Clock.Reset();
        var (_, sessionId, _) = await LoginAsync(await CreateUserAsync());

        var issued = (await IssueAsync(sessionId)).Value;

        Assert.Matches("^[A-Za-z0-9_-]{43}$", issued.Token);   // 32 random bytes, base64url
        var stored = await DbAsync(db => db.SessionRefreshTokens.AsNoTracking().SingleAsync(t => t.SessionId == sessionId));
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token)), stored.TokenHash);
        Assert.Equal(-1, stored.TokenHash.AsSpan().IndexOf(Encoding.UTF8.GetBytes(issued.Token)));
        Assert.Equal(0, await DbAsync(db => db.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM RefreshTokens WHERE CONVERT(nvarchar(max), CONVERT(varbinary(max), TokenHash), 1) LIKE {"%" + issued.Token + "%"}").SingleAsync()));

        // a session never gets a second usable token (the database refuses it)
        Assert.Equal("Auth.RefreshInvalid", (await IssueAsync(sessionId)).Error.Code);
        Assert.Equal(1, await UsableTokensAsync(sessionId));

        var rotated = (await RotateAsync(issued.Token)).Value;
        Assert.NotEqual(issued.Token, rotated.RefreshToken);
        Assert.Equal(sessionId, rotated.SessionId);
        Assert.Equal(sessionId.ToString(), Claim(rotated.AccessToken, "sid"));
        var successor = await DbAsync(db => db.SessionRefreshTokens.AsNoTracking().SingleAsync(t => t.SessionId == sessionId && t.ConsumedAt == null));
        Assert.Equal(stored.Id, successor.ParentId);
    }

    [SqlServerFact]
    public async Task Rotation_needs_no_access_token_and_works_long_after_it_expired()
    {
        Factory.Clock.Reset();
        var (_, sessionId, _) = await LoginAsync(await CreateUserAsync());
        var issued = (await IssueAsync(sessionId)).Value;

        Factory.Clock.Advance(TimeSpan.FromDays(2));   // far beyond the 15-minute access token
        var rotated = await RotateAsync(issued.Token);

        Assert.True(rotated.IsSuccess);
        Assert.Equal(900, rotated.Value.AccessTokenExpiresIn);
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(rotated.Value.AccessToken));
        Factory.Clock.Reset();
    }

    [SqlServerFact]
    public async Task Garbage_unknown_and_oversized_tokens_are_refused_without_effect()
    {
        Factory.Clock.Reset();
        foreach (var token in new[] { null, "", "not-a-token", new string('A', 43), new string('x', 5000) })
            Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(token!)).Error.Code);
    }

    // ---------------------------------------------------------------- grace, replay, attacker-first

    [SqlServerFact]
    public async Task Within_ten_seconds_a_consumed_token_is_a_race_and_after_that_a_replay_that_ends_the_session()
    {
        var start = DateTimeOffset.UtcNow;
        Factory.Clock.Set(start);
        var user = await CreateUserAsync();
        var (_, sessionId, _) = await LoginAsync(user);
        var original = (await IssueAsync(sessionId)).Value.Token;
        var rotated = (await RotateAsync(original)).Value;
        var tokensBefore = await DbAsync(db => db.SessionRefreshTokens.CountAsync(t => t.SessionId == sessionId));

        Factory.Clock.Set(start + SessionPolicy.RotationGrace);   // exactly 10 s: still a race, nothing issued
        Assert.Equal("Auth.RefreshRaced", (await RotateAsync(original)).Error.Code);
        Assert.Null((await SessionAsync(sessionId)).RevokedAt);
        Assert.Equal(tokensBefore, await DbAsync(db => db.SessionRefreshTokens.CountAsync(t => t.SessionId == sessionId)));

        Factory.Clock.Set(start + SessionPolicy.RotationGrace + TimeSpan.FromMilliseconds(1));   // just past: replay
        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(original)).Error.Code);

        var session = await SessionAsync(sessionId);
        Assert.Equal(SessionPolicy.RevokedReason.RefreshReuse, session.RevokedReason);
        Assert.Equal(0, await UsableTokensAsync(sessionId));
        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(rotated.RefreshToken)).Error.Code);   // the successor died too
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(rotated.AccessToken));             // and the access token
        var entry = await DbAsync(db => db.AuditLogs.AsNoTracking().SingleAsync(e => e.TargetId == sessionId.ToString() && e.Action == AuditActions.RefreshReuseDetected));
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
        Factory.Clock.Reset();
    }

    [SqlServerFact]
    public async Task An_attacker_who_rotates_first_loses_everything_when_the_victim_presents_the_old_token()
    {
        var start = DateTimeOffset.UtcNow;
        Factory.Clock.Set(start);
        var (_, sessionId, _) = await LoginAsync(await CreateUserAsync());
        var stolen = (await IssueAsync(sessionId)).Value.Token;

        var attacker = (await RotateAsync(stolen)).Value;   // the attacker wins with the stolen copy
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(attacker.AccessToken));

        Factory.Clock.Set(start + TimeSpan.FromSeconds(11));
        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(stolen)).Error.Code);   // the victim's next refresh

        Assert.NotNull((await SessionAsync(sessionId)).RevokedAt);
        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(attacker.RefreshToken)).Error.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(attacker.AccessToken));
        Factory.Clock.Reset();
    }

    // ---------------------------------------------------------------- expiry, disabled accounts, the 30 s check window

    [SqlServerFact]
    public async Task Expired_tokens_and_sessions_are_refused_without_being_treated_as_replays()
    {
        var start = DateTimeOffset.UtcNow;
        Factory.Clock.Set(start);
        var (access, sessionId, _) = await LoginAsync(await CreateUserAsync());
        var token = (await IssueAsync(sessionId)).Value.Token;

        Factory.Clock.Set(start + TimeSpan.FromDays(15));   // past the 14-day sliding expiry
        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(token)).Error.Code);
        Assert.Null((await SessionAsync(sessionId)).RevokedAt);   // not a replay

        Factory.Clock.Set(start + TimeSpan.FromDays(31));   // past the 30-day absolute expiry
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(access));
        Factory.Clock.Reset();
    }

    [SqlServerFact]
    public async Task A_disabled_account_cannot_rotate_and_a_revocation_made_elsewhere_applies_within_30_seconds()
    {
        var start = DateTimeOffset.UtcNow;
        Factory.Clock.Set(start);
        var user = await CreateUserAsync();
        var (access, sessionId, _) = await LoginAsync(user);
        var token = (await IssueAsync(sessionId)).Value.Token;
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(access));   // the check result is now cached

        // changed directly in the database (as another server process would): no cache eviction here
        await ExecuteAsync($"UPDATE dbo.AspNetUsers SET IsDisabled = 1 WHERE Id = '{user.Id}'");

        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(token)).Error.Code);
        Assert.Null((await DbAsync(db => db.SessionRefreshTokens.AsNoTracking().SingleAsync(t => t.SessionId == sessionId))).ConsumedAt);   // rolled back

        Factory.Clock.Set(start + TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(access));            // still inside the 30 s window
        Factory.Clock.Set(start + TimeSpan.FromSeconds(31));
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(access));  // refused after it
        Factory.Clock.Reset();
    }

    [SqlServerFact]
    public async Task A_failure_while_issuing_the_successor_rolls_the_rotation_back()
    {
        Factory.Clock.Reset();
        var (_, sessionId, _) = await LoginAsync(await CreateUserAsync());
        var token = (await IssueAsync(sessionId)).Value.Token;

        await ExecuteAsync("CREATE TRIGGER dbo.TR_Test_RefreshStoreDown ON dbo.RefreshTokens AFTER INSERT AS THROW 50002, N'refresh store unavailable (test)', 1;");
        try
        {
            Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(token)).Error.Code);
        }
        finally
        {
            await ExecuteAsync("DROP TRIGGER IF EXISTS dbo.TR_Test_RefreshStoreDown");
        }

        var original = await DbAsync(db => db.SessionRefreshTokens.AsNoTracking().SingleAsync(t => t.SessionId == sessionId));
        Assert.Null(original.ConsumedAt);                      // not consumed: the whole rotation was undone
        Assert.True((await RotateAsync(token)).IsSuccess);     // and works once the store is back
    }

    // ---------------------------------------------------------------- revocation hooks

    [SqlServerFact]
    public async Task A_password_change_ends_every_session_of_the_account()
    {
        Factory.Clock.Reset();
        var user = await CreateUserAsync();
        var (first, firstSession, _) = await LoginAsync(user);
        var (second, secondSession, _) = await LoginAsync(user);
        var refresh = (await IssueAsync(secondSession)).Value.Token;

        var response = await Bearer(first).PutAsJsonAsync("/api/account/change-password", new { currentPassword = Password, newPassword = "Changed#Passw0rd!6a" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(first));
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(second));
        Assert.Equal(SessionPolicy.RevokedReason.PasswordChanged, (await SessionAsync(firstSession)).RevokedReason);
        Assert.Equal("Auth.RefreshInvalid", (await RotateAsync(refresh)).Error.Code);
        Assert.Contains("\"revokedSessions\":2", await DbAsync(db => db.AuditLogs.Where(e => e.TargetId == user.Id && e.Action == AuditActions.PasswordChanged).Select(e => e.Details!).SingleAsync()));
    }

    [SqlServerFact]
    public async Task A_password_reset_ends_every_session_of_the_account()
    {
        Factory.Clock.Reset();
        var user = await CreateUserAsync();
        var (access, sessionId, _) = await LoginAsync(user);
        string code;
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(user.Id))!)));
        }

        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/Reset-Password", new { email = user.Email, code, newPassword = "Reset#Passw0rd!6a" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(access));
        Assert.Equal(SessionPolicy.RevokedReason.PasswordReset, (await SessionAsync(sessionId)).RevokedReason);
    }

    [SqlServerFact]
    public async Task Disabling_an_account_or_changing_its_roles_ends_its_sessions_immediately()
    {
        Factory.Clock.Reset();
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var (adminToken, _, _) = await LoginAsync(admin);

        var disabled = await CreateUserAsync();
        var (disabledToken, disabledSession, _) = await LoginAsync(disabled);
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(disabledToken));
        Assert.Equal(HttpStatusCode.NoContent, (await Bearer(adminToken).PutAsync($"/api/users/{disabled.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(disabledToken));   // evicted: no 30 s wait in this process
        Assert.Equal(SessionPolicy.RevokedReason.Disabled, (await SessionAsync(disabledSession)).RevokedReason);

        var demoted = await CreateUserAsync();
        var (demotedToken, demotedSession, _) = await LoginAsync(demoted);
        Assert.Equal(HttpStatusCode.NoContent, (await Bearer(adminToken).PutAsJsonAsync($"/api/users/{demoted.Id}",
            new { firstName = "S", lastName = "T", email = demoted.Email, roles = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(demotedToken));
        Assert.Equal(SessionPolicy.RevokedReason.RolesChanged, (await SessionAsync(demotedSession)).RevokedReason);

        // an edit that keeps the roles keeps the sessions
        var kept = await CreateUserAsync();
        var (keptToken, _, _) = await LoginAsync(kept);
        Assert.Equal(HttpStatusCode.NoContent, (await Bearer(adminToken).PutAsJsonAsync($"/api/users/{kept.Id}",
            new { firstName = "S", lastName = "T", email = kept.Email, roles = new[] { PlatformRoles.Candidate } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(keptToken));
    }

    // ---------------------------------------------------------------- the session API

    [SqlServerFact]
    public async Task Users_list_and_end_their_own_sessions_and_never_anyone_elses()
    {
        Factory.Clock.Reset();
        var user = await CreateUserAsync();
        var (phone, phoneSession, _) = await LoginAsync(user);
        var (laptop, laptopSession, _) = await LoginAsync(user);
        var (tablet, _, _) = await LoginAsync(user);
        var (stranger, strangerSession, _) = await LoginAsync(await CreateUserAsync());

        var list = JsonDocument.Parse(await Bearer(laptop).GetStringAsync("/api/v1/me/sessions")).RootElement;
        Assert.Equal(3, list.GetArrayLength());
        Assert.Equal(laptopSession, list.EnumerateArray().Single(s => s.GetProperty("current").GetBoolean()).GetProperty("id").GetGuid());
        Assert.Equal(new[] { "id", "createdAt", "lastSeenAt", "expiresAt", "method", "userAgent", "current" }.Order(),
            list[0].EnumerateObject().Select(p => p.Name).Order());

        // another user's session is "not found" and stays active
        Assert.Equal(HttpStatusCode.NotFound, (await Bearer(laptop).DeleteAsync($"/api/v1/me/sessions/{strangerSession}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(stranger));

        Assert.Equal(HttpStatusCode.NoContent, (await Bearer(laptop).DeleteAsync($"/api/v1/me/sessions/{phoneSession}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(phone));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(laptop));

        var others = await Bearer(laptop).PostAsync("/api/v1/me/sessions/revoke-others", null);
        Assert.Equal(1, JsonDocument.Parse(await others.Content.ReadAsStringAsync()).RootElement.GetProperty("revoked").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(tablet));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(laptop));

        Assert.Equal(HttpStatusCode.NoContent, (await Bearer(laptop).DeleteAsync("/api/v1/me/sessions/current")).StatusCode);   // sign-out
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(laptop));
        Assert.Equal(SessionPolicy.RevokedReason.Logout, (await SessionAsync(laptopSession)).RevokedReason);

        var actions = await DbAsync(db => db.AuditLogs.Where(e => e.ActorUserId == user.Id).Select(e => e.Action).ToListAsync());
        Assert.Equal(2, actions.Count(a => a == AuditActions.SessionRevoked));
        Assert.Single(actions, a => a == AuditActions.OtherSessionsRevoked);
    }

    [SqlServerFact]
    public async Task Concurrent_sign_outs_end_the_session_once_and_concurrent_sign_ins_create_separate_sessions()
    {
        Factory.Clock.Reset();
        var user = await CreateUserAsync();
        var (access, sessionId, _) = await LoginAsync(user);

        var signOuts = await Concurrently(5, () => Bearer(access).DeleteAsync("/api/v1/me/sessions/current"));

        Assert.Single(signOuts, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.All(signOuts.Where(r => r.StatusCode != HttpStatusCode.NoContent),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Unauthorized }));
        Assert.Equal(1, await DbAsync(db => db.AuditLogs.CountAsync(e => e.TargetId == sessionId.ToString() && e.Action == AuditActions.SessionRevoked)));

        var logins = await Concurrently(10, () => LoginAsync(user));
        Assert.Equal(10, logins.Select(l => l.SessionId).Distinct().Count());
        Assert.Equal(10, await DbAsync(db => db.UserSessions.CountAsync(s => s.UserId == user.Id && s.RevokedAt == null)));
    }

    // ---------------------------------------------------------------- helpers

    private static string TokenWithoutSession(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("c0-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz"));
        var token = new JwtSecurityToken("WazzufJobsAPI", "WazzufJobsClient",
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim("permissions", "[\"jobs:read\"]", JsonClaimValueTypes.JsonArray)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
