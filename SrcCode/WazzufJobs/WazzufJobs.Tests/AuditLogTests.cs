using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Audit;
using WazzufJobs.BLL.Features.Companies.Commands.ChangeMemberRole;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>A throwaway SQL Server host whose requests come from a fixed TCP peer (the audited client address).</summary>
public sealed class AuditApiFactory : SqlServerApiFactory
{
    public static readonly IPAddress PeerAddress = IPAddress.Parse("203.0.113.7");

    protected override string DatabasePrefix => "WazzufJobs_AuditTest_";

    protected override void ConfigureAdditional(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, FixedPeerAddress>());

    private sealed class FixedPeerAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = PeerAddress;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

/// <summary>
/// The audit log (Phase 02, S5; invariant P02-3): completeness and exactly one entry per privileged action, actor,
/// target, outcome, correlation id and hashed address, redaction, append-only storage, atomic commit with the change,
/// audit failures that never weaken a decision, concurrency, and no way to read the log over the API.
/// </summary>
public class AuditLogTests(AuditApiFactory factory) : IClassFixture<AuditApiFactory>
{
    private const string Password = "Audit#Passw0rd!S5";
    private const string UserAgent = "WazzufJobs-AuditTests/1.0";
    private static readonly byte[] TestKey = Convert.FromBase64String("d2F6enVmam9icy10ZXN0LW9ubHktYXVkaXQta2V5ISE=");
    private static readonly byte[] ExpectedIpHash = new HmacIpHasher(TestKey).Hash(AuditApiFactory.PeerAddress)!;

    // ---------------------------------------------------------------- sign-in

    [SqlServerFact]
    public async Task Sign_in_events_are_recorded_and_unknown_accounts_leave_no_trace()
    {
        var user = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);

        var success = await OneEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password }));
        Assert.Equal(AuditActions.LoginSucceeded, success.Entry.Action);
        AssertEntry(success.Entry, AuditOutcome.Success, actor: user.Id, AuditTargets.User, user.Id);

        var failed = await OneEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Wrong#Passw0rd1" }));
        Assert.Equal(AuditActions.LoginFailed, failed.Entry.Action);
        AssertEntry(failed.Entry, AuditOutcome.Failure, actor: null, AuditTargets.User, user.Id);
        Assert.Contains("invalid_password", failed.Entry.Details);

        // an unknown email: no entry, and exactly the response of a wrong password for an existing account
        var unknown = await NoEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/login", new { email = $"nobody-{Guid.NewGuid():N}@example.test", password = "Wrong#Passw0rd1" }));
        Assert.Equal(failed.Response.StatusCode, unknown.StatusCode);
        Assert.Equal(await failed.Response.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        // lockout (Identity: 5 failures)
        for (var i = 0; i < 4; i++)
            await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Wrong#Passw0rd1" });
        Assert.Contains(await EntriesAsync(e => e.TargetId == user.Id), e => e.Action == AuditActions.LoginLockedOut && e.Outcome == AuditOutcome.Failure);

        // password-reset requests: existing accounts are recorded, unknown ones are not; both answer the same
        var known = await OneEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/forget-Password", new { email = user.Email }));
        Assert.Equal(AuditActions.PasswordResetRequested, known.Entry.Action);
        var none = await NoEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/forget-Password", new { email = $"nobody-{Guid.NewGuid():N}@example.test" }));
        Assert.Equal(known.Response.StatusCode, none.StatusCode);
    }

    [SqlServerFact]
    public async Task Account_lifecycle_events_are_recorded()
    {
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        var registered = await OneEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, firstName = "Audit", lastName = "Register" }));
        Assert.Equal(AuditActions.UserRegistered, registered.Entry.Action);

        string userId, code;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            userId = user.Id;
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        }
        Assert.Equal(userId, registered.Entry.ActorUserId);

        var confirmed = await OneEntryAsync(() => Anonymous().GetAsync($"/api/auth/Confirm-Email?UserId={userId}&Code={code}"));
        Assert.Equal(AuditActions.EmailConfirmed, confirmed.Entry.Action);
        Assert.Contains("Candidate", confirmed.Entry.Details);

        var login = await (await Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password = Password })).Content.ReadFromJsonAsync<LoginResult>();
        var client = Bearer(login!.Token);

        var wrong = await OneEntryAsync(() => client.PutAsJsonAsync("/api/account/change-password", new { currentPassword = "Wrong#Passw0rd1", newPassword = "Changed#Passw0rd2" }));
        Assert.Equal(AuditActions.PasswordChangeFailed, wrong.Entry.Action);
        AssertEntry(wrong.Entry, AuditOutcome.Failure, actor: userId, AuditTargets.User, userId);

        var changed = await OneEntryAsync(() => client.PutAsJsonAsync("/api/account/change-password", new { currentPassword = Password, newPassword = "Changed#Passw0rd2" }));
        Assert.Equal(AuditActions.PasswordChanged, changed.Entry.Action);
        Assert.Contains("\"revokedTokens\":1", changed.Entry.Details);

        var reset = await OneEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/Reset-Password", new { email, code = WebEncoders.Base64UrlEncode("bad-code"u8.ToArray()), newPassword = "Reset#Passw0rd3" }));
        Assert.Equal(AuditActions.PasswordResetFailed, reset.Entry.Action);

        var again = await (await Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password = "Changed#Passw0rd2" })).Content.ReadFromJsonAsync<LoginResult>();

        // pre-existing (P01, finding F-S5-1): the legacy revoke endpoint never finds the token (the user's tokens are not
        // loaded), so it fails and writes nothing. The handler with the tokens loaded records the sign-out.
        var http = await NoEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/revoke-refresh-token", new { token = again!.Token, refreshToken = again.RefreshToken }));
        Assert.Contains("User.RefreshTokenNotFound", await http.Content.ReadAsStringAsync());
        var logout = await OneEntryAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().RefreshTokens.Where(t => t.UserId == userId).LoadAsync();
            return await scope.ServiceProvider.GetRequiredService<MediatR.IMediator>()
                .Send(new BLL.Features.Auth.Command.RevokeRefreshToken.RevokeRefreshTokenCommand(again!.Token, again.RefreshToken));
        });
        Assert.True(logout.Response.IsSuccess);
        Assert.Equal(AuditActions.Logout, logout.Entry.Action);
        Assert.Equal(userId, logout.Entry.ActorUserId);
    }

    // ---------------------------------------------------------------- administration

    [SqlServerFact]
    public async Task Every_user_administration_action_writes_exactly_one_entry()
    {
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var client = Client(TestPersonas.SuperAdmin, admin);
        var email = $"managed-{Guid.NewGuid():N}@example.test";

        var created = await OneEntryAsync(() => client.PostAsJsonAsync("/api/users", new { firstName = "M", lastName = "U", email, password = Password, roles = new[] { "Candidate" } }));
        Assert.Equal(AuditActions.UserCreated, created.Entry.Action);
        var id = created.Entry.TargetId!;
        AssertEntry(created.Entry, AuditOutcome.Success, actor: admin.Id, AuditTargets.User, id);
        Assert.Contains("Candidate", created.Entry.Details);

        var newEmail = $"renamed-{Guid.NewGuid():N}@example.test";
        var updated = await OneEntryAsync(() => client.PutAsJsonAsync($"/api/users/{id}", new { firstName = "M", lastName = "U", email = newEmail, roles = new[] { "Candidate" } }));
        Assert.Equal(AuditActions.UserUpdated, updated.Entry.Action);
        Assert.Contains("\"emailChanged\":true", updated.Entry.Details);
        Assert.DoesNotContain(newEmail, updated.Entry.Details ?? "");

        var roles = await OneEntryAsync(() => client.PutAsJsonAsync($"/api/users/{id}", new { firstName = "M", lastName = "U", email = newEmail, roles = Array.Empty<string>() }));
        Assert.Equal(AuditActions.UserRolesChanged, roles.Entry.Action);
        Assert.Contains("\"rolesRemoved\":[\"Candidate\"]", roles.Entry.Details);

        Assert.Equal(AuditActions.UserDisabled, (await OneEntryAsync(() => client.PutAsync($"/api/users/{id}/toggle-status", null))).Entry.Action);
        Assert.Equal(AuditActions.UserEnabled, (await OneEntryAsync(() => client.PutAsync($"/api/users/{id}/toggle-status", null))).Entry.Action);
        Assert.Equal(AuditActions.UserUnlocked, (await OneEntryAsync(() => client.PutAsync($"/api/users/{id}/unlock", null))).Entry.Action);

        var denied = await OneEntryAsync(() => client.PutAsJsonAsync($"/api/users/{id}", new { firstName = "M", lastName = "U", email = newEmail, roles = new[] { "SuperAdmin" } }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.Response.StatusCode);
        Assert.Equal(AuditActions.UserChangeDenied, denied.Entry.Action);
        AssertEntry(denied.Entry, AuditOutcome.Denied, actor: admin.Id, AuditTargets.User, id);
        Assert.Contains("Users.StaffRolesManagedSeparately", denied.Entry.Details);

        var role = await OneEntryAsync(() => client.PutAsJsonAsync($"/api/roles/{PermissionCatalog.Candidate.Id}", new { name = "Candidate", permissions = Permissions.All }));
        Assert.Equal(AuditActions.RolesChangeDenied, role.Entry.Action);
        Assert.Equal(PermissionCatalog.Candidate.Id, role.Entry.TargetId);

        // a request that changes nothing and breaks no rule writes nothing
        await NoEntryAsync(() => client.PutAsync($"/api/users/{Guid.NewGuid()}/toggle-status", null));
    }

    [SqlServerFact]
    public async Task Platform_administration_and_cv_access_are_recorded_with_the_company()
    {
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var client = Client(TestPersonas.SuperAdmin, admin);

        var category = await OneEntryAsync(() => client.PostAsync("/api/categories",
            new MultipartFormDataContent { { new StringContent($"Audit {Guid.NewGuid():N}"[..20]), "Name" } }));
        Assert.Equal(AuditActions.CategoryCreated, category.Entry.Action);
        var categoryId = category.Entry.TargetId!;
        Assert.Equal(AuditActions.CategoryUpdated, (await OneEntryAsync(() => client.PutAsync($"/api/categories/{categoryId}",
            new MultipartFormDataContent { { new StringContent($"Audit2 {Guid.NewGuid():N}"[..20]), "Name" } }))).Entry.Action);

        var job = await OneEntryAsync(() => client.PostAsJsonAsync("/api/jobs", JobPayload(int.Parse(categoryId))));
        Assert.Equal(AuditActions.JobCreated, job.Entry.Action);
        var jobId = job.Entry.TargetId!;
        Assert.Equal(AuditActions.JobUpdated, (await OneEntryAsync(() => client.PutAsJsonAsync($"/api/jobs/{jobId}", JobPayload(int.Parse(categoryId))))).Entry.Action);
        var toggled = await OneEntryAsync(() => client.PutAsync($"/api/jobs/{jobId}/toggle-status", null));
        Assert.Equal(AuditActions.JobStatusChanged, toggled.Entry.Action);
        Assert.Contains("\"to\":\"Closed\"", toggled.Entry.Details);
        Assert.Equal(AuditActions.JobDeleted, (await OneEntryAsync(() => client.DeleteAsync($"/api/jobs/{jobId}"))).Entry.Action);
        Assert.Equal(AuditActions.CategoryDeleted, (await OneEntryAsync(() => client.DeleteAsync($"/api/categories/{categoryId}"))).Entry.Action);

        var s = await ApplicationWithCvAsync(admin);
        var status = await OneEntryAsync(() => client.PutAsJsonAsync($"/api/applications/{s.ApplicationOfA.Id}/status", new { status = (int)ApplicationStatus.Shortlisted }));
        Assert.Equal(AuditActions.ApplicationStatusChanged, status.Entry.Action);
        Assert.Equal(s.Job.CompanyId, status.Entry.CompanyId);
        Assert.Contains("\"to\":\"Shortlisted\"", status.Entry.Details);

        var cv = await OneEntryAsync(() => client.GetAsync($"/api/applications/{s.ApplicationOfA.Id}/cv"));
        Assert.Equal(HttpStatusCode.OK, cv.Response.StatusCode);
        Assert.Equal(AuditActions.ApplicationCvAccessed, cv.Entry.Action);
        AssertEntry(cv.Entry, AuditOutcome.Success, actor: admin.Id, AuditTargets.Application, s.ApplicationOfA.Id.ToString());
        Assert.Equal(s.Job.CompanyId, cv.Entry.CompanyId);
        Assert.Null(cv.Entry.Details);
    }

    [SqlServerFact]
    public async Task Authorization_and_company_denials_are_recorded_but_anonymous_requests_are_not()
    {
        var candidate = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);

        var denied = await OneEntryAsync(() => Client(TestPersonas.Candidate, candidate).GetAsync($"/api/users/{candidate.Id}?probe=secret-value"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.Response.StatusCode);
        Assert.Equal(AuditActions.AuthorizationDenied, denied.Entry.Action);
        AssertEntry(denied.Entry, AuditOutcome.Denied, actor: candidate.Id, AuditTargets.Endpoint, null);
        Assert.Equal("{\"method\":\"GET\",\"permissions\":[\"users:read\"],\"route\":\"api/Users/{userId}\"}", denied.Entry.Details);

        await NoEntryAsync(() => Anonymous().GetAsync("/api/users"));

        var company = Guid.NewGuid();
        var foreign = await OneEntryAsync(() => Client(TestPersonas.Candidate, candidate).GetAsync($"/api/v1/companies/{company}"));
        Assert.Equal(HttpStatusCode.NotFound, foreign.Response.StatusCode);
        Assert.Equal(AuditActions.CompanyAccessDenied, foreign.Entry.Action);
        Assert.Equal(company, foreign.Entry.CompanyId);
        Assert.Contains("Company.NotFound", foreign.Entry.Details);
    }

    [SqlServerFact]
    public async Task Company_team_changes_and_refusals_are_recorded_with_the_company()
    {
        var (company, owner, recruiter) = await CompanyAsync();

        var changed = await OneEntryAsync(() => ChangeRoleAsync(owner, company, recruiter.Id, CompanyMemberRole.Admin));
        Assert.Equal(AuditActions.CompanyMemberRoleChanged, changed.Entry.Action);
        AssertEntry(changed.Entry, AuditOutcome.Success, actor: owner.Id, AuditTargets.CompanyMember, recruiter.Id, viaHttp: false);
        Assert.Equal(company, changed.Entry.CompanyId);
        Assert.Equal("{\"from\":\"Recruiter\",\"to\":\"Admin\"}", changed.Entry.Details);

        var refused = await OneEntryAsync(() => ChangeRoleAsync(owner, company, owner.Id, CompanyMemberRole.Admin));
        Assert.False(refused.Response.IsSuccess);
        Assert.Equal(AuditActions.CompanyMemberChangeDenied, refused.Entry.Action);
        Assert.Contains("Company.LastOwner", refused.Entry.Details);
    }

    // ---------------------------------------------------------------- redaction and append-only storage

    [SqlServerFact]
    public async Task No_entry_contains_secrets_tokens_personal_data_or_plaintext_addresses()
    {
        var user = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);
        var login = await (await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password })).Content.ReadFromJsonAsync<LoginResult>();
        await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Wrong#Passw0rd1" });
        await Bearer(login!.Token).PutAsJsonAsync("/api/account/change-password", new { currentPassword = Password, newPassword = "Changed#Passw0rd2" });
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var s = await ApplicationWithCvAsync(admin);
        await Client(TestPersonas.SuperAdmin, admin).GetAsync($"/api/applications/{s.ApplicationOfA.Id}/cv");

        var forbidden = new[]
        {
            Password, "Wrong#Passw0rd1", "Changed#Passw0rd2", login.Token, login.RefreshToken, "eyJ", "@",
            AuditApiFactory.PeerAddress.ToString(), s.CvOfA.StorageKey, "cloudinary", TestData.SecretFeedback, "C# developer"
        };
        var violations = (await EntriesAsync(_ => true))
            .SelectMany(e => new[] { e.Action, e.ActorUserId, e.TargetType, e.TargetId, e.UserAgent, e.CorrelationId, e.Details }
                .Where(v => v is not null)
                .SelectMany(v => forbidden.Where(f => v!.Contains(f, StringComparison.OrdinalIgnoreCase)).Select(f => $"#{e.Id} {e.Action}: '{f}'")))
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
        Assert.All(await EntriesAsync(e => e.IpHash != null), e => Assert.Equal(ExpectedIpHash, e.IpHash));
    }

    [SqlServerFact]
    public async Task Entries_cannot_be_changed_or_deleted_through_ef_or_sql()
    {
        var user = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);
        var entry = (await OneEntryAsync(() => Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Wrong#Passw0rd1" }))).Entry;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var tracked = await db.AuditLogs.SingleAsync(e => e.Id == entry.Id);
            tracked.Action = "tampered";
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.AuditLogs.Remove(await db.AuditLogs.SingleAsync(e => e.Id == entry.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var sql = await Assert.ThrowsAsync<SqlException>(() => db.AuditLogs.Where(e => e.Id == entry.Id).ExecuteDeleteAsync());
            Assert.Equal(51000, sql.Number);
        }

        var reloaded = (await EntriesAsync(e => e.Id == entry.Id)).Single();
        Assert.Equal(entry.Action, reloaded.Action);
        Assert.Equal(entry.Details, reloaded.Details);
    }

    [Fact]
    public void Production_code_only_ever_appends_audit_entries_and_nothing_reads_them()
    {
        var root = SolutionDirectory();
        var uses = new[] { "WazzufJobs.API", "WazzufJobs.BLL", "WazzufJobs.DAL" }
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select(line => (File: Path.GetFileName(f), Line: line.Trim())))
            .Where(x => x.Line.Contains("AuditLogs") && !x.Line.StartsWith("//") && !x.Line.StartsWith("///") && !x.Line.StartsWith("*"))
            .ToList();

        Assert.All(uses, u => Assert.True(
            u.File == "ApplicationDBContext.cs" || (u.File == "PlatformConfiguration.cs" && u.Line.Contains("TR_AuditLogs_AppendOnly"))
                || (u.File == "AuditRepository.cs" && u.Line.Contains("AuditLogs.Add(")),
            $"{u.File}: {u.Line}"));
        Assert.All(typeof(IAuditRepository).GetMethods(), m => Assert.Matches("^(Append|AppendSeparately|RunInTransaction)Async$", m.Name));
    }

    [SqlServerFact]
    public void No_endpoint_exposes_the_audit_log()
    {
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? "").ToList();

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, r => r.Contains("audit", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------- atomicity and audit-store failures

    [SqlServerFact]
    public async Task A_change_whose_entry_cannot_be_written_is_rolled_back()
    {
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var target = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);
        var client = Client(TestPersonas.SuperAdmin, admin);
        int jobsBefore;
        using (var scope = factory.Services.CreateScope())
            jobsBefore = await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().Jobs.CountAsync();

        await using (await FailingAuditAsync(AuditActions.UserDisabled, AuditActions.JobCreated))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PutAsync($"/api/users/{target.Id}/toggle-status", null)).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync("/api/jobs", JobPayload(1))).StatusCode);
        }

        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        Assert.False((await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).IsDisabled);
        Assert.Equal(jobsBefore, await db.Jobs.CountAsync());
        Assert.Empty(await EntriesAsync(e => e.TargetId == target.Id && e.Action == AuditActions.UserDisabled));

        // with the audit store back, the same change succeeds together with its entry
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/users/{target.Id}/toggle-status", null)).StatusCode);
        Assert.Single(await EntriesAsync(e => e.TargetId == target.Id && e.Action == AuditActions.UserDisabled));
    }

    [SqlServerFact]
    public async Task Without_an_audit_entry_no_sign_in_succeeds_and_no_cv_is_released()
    {
        var user = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var s = await ApplicationWithCvAsync(admin);

        await using (await FailingAuditAsync(AuditActions.LoginSucceeded, AuditActions.ApplicationCvAccessed))
        {
            var login = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });
            Assert.Equal(HttpStatusCode.InternalServerError, login.StatusCode);
            Assert.DoesNotContain("token", await login.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var cv = await Client(TestPersonas.SuperAdmin, admin).GetAsync($"/api/applications/{s.ApplicationOfA.Id}/cv");
            Assert.Equal(HttpStatusCode.InternalServerError, cv.StatusCode);
            Assert.DoesNotContain("%PDF", await cv.Content.ReadAsStringAsync());
        }

        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().RefreshTokens.CountAsync(t => t.UserId == user.Id));
    }

    [SqlServerFact]
    public async Task An_audit_outage_never_changes_a_refusal_or_reveals_an_account()
    {
        var user = await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate);
        var unknownResponse = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = $"nobody-{Guid.NewGuid():N}@example.test", password = "Wrong#Passw0rd1" });
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        var resetUnknown = await Anonymous().PostAsJsonAsync("/api/auth/forget-Password", new { email = $"nobody-{Guid.NewGuid():N}@example.test" });

        await using (await FailingAuditAsync(AuditActions.AuthorizationDenied, AuditActions.LoginFailed, AuditActions.CompanyAccessDenied,
                         AuditActions.PasswordResetRequested, AuditActions.UserChangeDenied))
        {
            var client = Client(TestPersonas.Candidate, user);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}")).StatusCode);

            var wrong = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Wrong#Passw0rd1" });
            Assert.Equal(unknownResponse.StatusCode, wrong.StatusCode);
            Assert.Equal(unknownBody, await wrong.Content.ReadAsStringAsync());
            Assert.Equal(resetUnknown.StatusCode, (await Anonymous().PostAsJsonAsync("/api/auth/forget-Password", new { email = user.Email })).StatusCode);

            var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
            Assert.Equal(HttpStatusCode.Forbidden, (await Client(TestPersonas.SuperAdmin, admin).PutAsJsonAsync($"/api/users/{user.Id}",
                new { firstName = "x", lastName = "y", email = user.Email, roles = new[] { "SuperAdmin" } })).StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Equal(["Candidate"], await users.GetRolesAsync((await users.FindByIdAsync(user.Id))!));
    }

    // ---------------------------------------------------------------- concurrency

    [SqlServerFact]
    public async Task Concurrent_audited_changes_each_commit_exactly_one_entry()
    {
        var admin = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var targets = new List<AppUser>();
        for (var i = 0; i < 20; i++)
            targets.Add(await CreateUserAsync(AccountType.Candidate, PlatformRoles.Candidate));

        var responses = await Task.WhenAll(targets.Select(t => Client(TestPersonas.SuperAdmin, admin).PutAsync($"/api/users/{t.Id}/toggle-status", null)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        var ids = targets.Select(t => t.Id).ToList();
        var entries = await EntriesAsync(e => ids.Contains(e.TargetId!) && e.Action == AuditActions.UserDisabled);
        Assert.Equal(20, entries.Count);
        Assert.Equal(20, entries.Select(e => e.TargetId).Distinct().Count());
        Assert.Equal(20, entries.Select(e => e.CorrelationId).Distinct().Count());
    }

    [SqlServerFact]
    public async Task Racing_super_admins_produce_one_change_entry_and_one_denial()
    {
        for (var round = 0; round < 5; round++)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                foreach (var existing in await users.GetUsersInRoleAsync(PlatformRoles.SuperAdmin))
                    await users.RemoveFromRoleAsync(existing, PlatformRoles.SuperAdmin);
            }
            var a = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
            var b = await CreateUserAsync(AccountType.Staff, PlatformRoles.SuperAdmin);

            await Task.WhenAll(
                Client(TestPersonas.SuperAdmin, a).PutAsync($"/api/users/{b.Id}/toggle-status", null),
                Client(TestPersonas.SuperAdmin, b).PutAsync($"/api/users/{a.Id}/toggle-status", null));

            var entries = await EntriesAsync(e => e.TargetId == a.Id || e.TargetId == b.Id);
            Assert.Single(entries, e => e.Action == AuditActions.UserDisabled && e.Outcome == AuditOutcome.Success);
            var denial = Assert.Single(entries, e => e.Action == AuditActions.UserChangeDenied);
            Assert.Contains("User.LastAdministrator", denial.Details);
        }
    }

    // ---------------------------------------------------------------- helpers

    private HttpClient Anonymous()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    private HttpClient Client(Persona persona, AppUser user) => Anonymous().As(factory.Services, persona, user);

    private HttpClient Bearer(string token)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertEntry(AuditLog entry, AuditOutcome outcome, string? actor, string targetType, string? targetId, bool viaHttp = true)
    {
        Assert.Equal(outcome, entry.Outcome);
        Assert.Equal(actor, entry.ActorUserId);
        Assert.Equal(actor is null ? AuditActorKind.Anonymous : AuditActorKind.User, entry.ActorKind);
        Assert.Equal(targetType, entry.TargetType);
        Assert.Equal(targetId, entry.TargetId);
        Assert.InRange(entry.OccurredAt, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(1));
        if (!viaHttp)
            return;
        Assert.Equal(ExpectedIpHash, entry.IpHash);
        Assert.Equal(UserAgent, entry.UserAgent);
        Assert.False(string.IsNullOrEmpty(entry.CorrelationId));
    }

    private async Task<List<AuditLog>> EntriesAsync(System.Linq.Expressions.Expression<Func<AuditLog, bool>> filter)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().AuditLogs.AsNoTracking().Where(filter).OrderBy(e => e.Id).ToListAsync();
    }

    private async Task<long> LastIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().AuditLogs.MaxAsync(e => (long?)e.Id) ?? 0;
    }

    /// <summary>Runs the action and asserts that it wrote exactly one audit entry (tests in this class run one at a time).</summary>
    private async Task<(T Response, AuditLog Entry)> OneEntryAsync<T>(Func<Task<T>> action)
    {
        var before = await LastIdAsync();
        var response = await action();
        var entries = await EntriesAsync(e => e.Id > before);
        var status = response is HttpResponseMessage http ? $" (HTTP {(int)http.StatusCode}: {await http.Content.ReadAsStringAsync()})" : "";
        Assert.True(entries.Count == 1, $"expected exactly one audit entry, found: [{string.Join(", ", entries.Select(e => e.Action))}]{status}");
        return (response, entries[0]);
    }

    private async Task<T> NoEntryAsync<T>(Func<Task<T>> action)
    {
        var before = await LastIdAsync();
        var response = await action();
        Assert.Empty(await EntriesAsync(e => e.Id > before));
        return response;
    }

    /// <summary>Makes the audit store reject the given actions (a test-only trigger) until disposed.</summary>
    private async Task<IAsyncDisposable> FailingAuditAsync(params string[] actions)
    {
        var list = string.Join(", ", actions.Select(a => $"N'{a}'"));
        await ExecuteAsync($"""
            CREATE TRIGGER dbo.TR_Test_AuditStoreDown ON dbo.AuditLogs AFTER INSERT AS
            IF EXISTS (SELECT 1 FROM inserted WHERE Action IN ({list})) THROW 50001, N'audit store unavailable (test)', 1;
            """);
        return new Restore(() => ExecuteAsync("DROP TRIGGER IF EXISTS dbo.TR_Test_AuditStoreDown"));
    }

    private sealed class Restore(Func<Task> restore) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await restore();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<AppUser> CreateUserAsync(AccountType accountType, string role)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"audit-{Guid.NewGuid():N}@example.test";
        var user = new AppUser { UserName = email, Email = email, FirstName = "Audit", LastName = "Test", EmailConfirmed = true, AccountType = accountType };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    private async Task<(Guid Company, AppUser Owner, AppUser Recruiter)> CompanyAsync()
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Audited", Slug = $"audited-{Guid.NewGuid():N}" };
        var owner = TestPersonas.CompanyUser.NewUser();
        var recruiter = TestPersonas.CompanyUser.NewUser();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        db.AddRange(company, owner, recruiter,
            new CompanyMember { CompanyId = company.Id, UserId = owner.Id, Role = CompanyMemberRole.Owner },
            new CompanyMember { CompanyId = company.Id, UserId = recruiter.Id, Role = CompanyMemberRole.Recruiter });
        await db.SaveChangesAsync();
        return (company.Id, owner, recruiter);
    }

    private async Task<BLL.Abstractions.Result> ChangeRoleAsync(AppUser actor, Guid companyId, string userId, CompanyMemberRole role)
    {
        using var scope = factory.Services.CreateScope();
        var acting = new ActingUser(actor.Id);
        var handler = new ChangeCompanyMemberRoleCommandHandler(acting.CompanyAccess(scope.ServiceProvider),
            scope.ServiceProvider.GetRequiredService<ICompanyRepository>(), acting.Audit(scope.ServiceProvider));
        return await handler.Handle(new ChangeCompanyMemberRoleCommand(companyId, userId, role), CancellationToken.None);
    }

    private sealed record SeededApplication(Job Job, Application ApplicationOfA, Resume CvOfA);

    /// <summary>A candidate's application with a CV to a company's job (unique storage key per call).</summary>
    private async Task<SeededApplication> ApplicationWithCvAsync(AppUser admin)
    {
        var candidate = TestPersonas.Candidate.NewUser();
        var company = new Company { Id = Guid.NewGuid(), Name = "Employer", Slug = $"employer-{Guid.NewGuid():N}" };
        var job = new Job { Title = "Backend", Description = "APIs", Location = "Cairo", Skills = ["C#"], CategoryId = 1, PostedById = admin.Id, Company = company, Status = JobStatus.Active };
        var cv = new Resume
        {
            Candidate = candidate, StorageKey = $"wazzuf-jobs/cvs/audit-{Guid.NewGuid():N}", StorageAccess = ResumeStorageAccess.Authenticated,
            FileName = "cv.pdf", Sha256 = new byte[32], IsDefault = true, Text = new ResumeText { ExtractedText = "C# developer" }
        };
        var application = new Application { User = candidate, Job = job, Company = company, Resume = cv, AIScore = 77, AIFeedback = TestData.SecretFeedback, IsAIScored = true };
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        db.AddRange(candidate, company, job, cv, application);
        await db.SaveChangesAsync();
        return new SeededApplication(job, application, cv);
    }

    private static object JobPayload(int categoryId) => new
    {
        title = $"Audited {Guid.NewGuid():N}"[..20],
        description = "Build APIs",
        location = "Cairo",
        skills = new[] { "C#" },
        jobType = 1,
        workplaceType = 2,
        categoryId,
        salaryMin = 1000,
        salaryMax = 2000,
        expiresAt = (DateTime?)null
    };

    private static string SolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WazzufJobs.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("WazzufJobs.sln not found");
    }

    private record LoginResult(string Token, string RefreshToken);
}
