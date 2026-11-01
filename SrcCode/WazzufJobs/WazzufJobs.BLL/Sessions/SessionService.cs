using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WazzufJobs.DAL.Enums;
using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Sessions;

public sealed record IssuedRefreshToken(string Token, DateTime ExpiresAt);

/// <summary>The result of a successful rotation: the successor refresh token and a new access token for the session.</summary>
public sealed record RotatedSession(Guid SessionId, string UserId, string RefreshToken, DateTime RefreshTokenExpiresAt, string AccessToken, int AccessTokenExpiresIn);

/// <summary>
/// Server-side sessions and refresh-token rotation (Phase 02, S6a; architecture §5.1–5.2). Transport-neutral: it takes
/// and returns raw tokens; how they travel (the httpOnly cookie) is added in S6b. Only SHA-256 hashes are stored.
/// </summary>
public interface ISessionService
{
    /// <summary>Starts a session for a successful sign-in (no refresh token yet; S6b issues it at hand-off).</summary>
    Task<UserSession> StartAsync(AppUser user, string amr, CancellationToken cancellationToken);

    /// <summary>Issues the first refresh token of an active session. Fails if the session already has a usable token.</summary>
    Task<Result<IssuedRefreshToken>> IssueRefreshTokenAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomic rotation: exactly one caller wins per token and receives a successor and an access token. A token consumed
    /// at most 10 s ago gives 409 <c>Auth.RefreshRaced</c> and nothing else; older reuse revokes the session (audited);
    /// anything else is 401 <c>Auth.RefreshInvalid</c>. Does not need an access token, so it works after expiry.
    /// </summary>
    Task<Result<RotatedSession>> RotateAsync(string? refreshToken, CancellationToken cancellationToken);

    /// <summary>The session check: the session is the user's, unrevoked, unexpired, and the account enabled (≤ 30 s cache).</summary>
    Task<bool> IsActiveAsync(Guid sessionId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSession>> ListActiveAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Revokes one session of the user, or all except <paramref name="except"/>; evicts the session-check cache.</summary>
    Task<IReadOnlyList<Guid>> RevokeAsync(string userId, Guid? sessionId, Guid? except, string reason, CancellationToken cancellationToken);
}

public sealed class SessionService(
    ISessionRepository sessions,
    IUserRepository users,
    IJwtProvider jwt,
    IAuditLog audit,
    IMemoryCache cache,
    TimeProvider clock,
    IRequestInfo request,
    IIpHasher ipHasher) : ISessionService
{
    private const int MaxTokenLength = 128;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public async Task<UserSession> StartAsync(AppUser user, string amr, CancellationToken cancellationToken)
    {
        var now = Now;
        var agent = request.UserAgent is { } ua ? new string(ua.Where(c => !char.IsControl(c)).Take(200).ToArray()) : null;
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = now,
            LastSeenAt = now,
            AbsoluteExpiresAt = now + SessionPolicy.Lifetime(user.AccountType).Absolute,
            Amr = amr,
            UserAgent = string.IsNullOrEmpty(agent) ? null : agent,
            IpHash = ipHasher.Hash(request.RemoteIpAddress)
        };
        await sessions.AddSessionAsync(session, cancellationToken);
        return session;
    }

    public async Task<Result<IssuedRefreshToken>> IssueRefreshTokenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var now = Now;
        IssuedRefreshToken? issued = null;
        try
        {
            await sessions.RunInTransactionAsync(async () =>
            {
                if (!await sessions.TouchActiveSessionAsync(sessionId, now, cancellationToken))
                    return false;
                var session = (await sessions.FindSessionAsync(sessionId, cancellationToken))!;
                issued = await InsertTokenAsync(session, parentId: null, now, cancellationToken);
                return true;
            }, ok => ok, cancellationToken);
        }
        catch (DbUpdateException)
        {
            issued = null;   // the session already has a usable token (filtered unique index)
        }

        return issued is null ? Result.Failure<IssuedRefreshToken>(SessionErrors.RefreshInvalid) : Result.Success(issued);
    }

    private enum Rotation { Lost, Rejected, Rotated }

    public async Task<Result<RotatedSession>> RotateAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken) || refreshToken.Length > MaxTokenLength)
            return Result.Failure<RotatedSession>(SessionErrors.RefreshInvalid);

        var now = Now;
        var hash = Hash(refreshToken);
        UserSession? session = null;
        IssuedRefreshToken? successor = null;

        Rotation outcome;
        try
        {
            outcome = await sessions.RunInTransactionAsync(async () =>
            {
                // 1. compare-and-swap: exactly one concurrent caller consumes the token
                var consumed = await sessions.TryConsumeAsync(hash, now, cancellationToken);
                if (consumed is null)
                    return Rotation.Lost;

                // 2. the session must still be usable (and the account enabled), or everything rolls back
                if (!await sessions.TouchActiveSessionAsync(consumed.SessionId, now, cancellationToken))
                    return Rotation.Rejected;

                // 3. the successor; the filtered unique index refuses a second usable token for the session
                session = (await sessions.FindSessionAsync(consumed.SessionId, cancellationToken))!;
                successor = await InsertTokenAsync(session, consumed.Id, now, cancellationToken);
                return Rotation.Rotated;
            }, o => o == Rotation.Rotated, cancellationToken);
        }
        catch (DbUpdateException)
        {
            outcome = Rotation.Rejected;   // rolled back: nothing issued
        }

        if (outcome == Rotation.Rejected)
            return Result.Failure<RotatedSession>(SessionErrors.RefreshInvalid);
        if (outcome == Rotation.Lost)
            return Result.Failure<RotatedSession>(await ClassifyLoserAsync(hash, now, cancellationToken));

        var user = (await users.FindByIdAsync(session!.UserId))!;
        var grant = PermissionCatalog.Resolve(user.AccountType, user.IsDemo, await users.GetRolesAsync(user));
        var (accessToken, expiresIn) = jwt.GenerateToken(user, grant, session.Id, session.Amr);
        return Result.Success(new RotatedSession(session.Id, user.Id, successor!.Token, successor.ExpiresAt, accessToken, expiresIn));
    }

    private async Task<Error> ClassifyLoserAsync(byte[] hash, DateTime now, CancellationToken cancellationToken)
    {
        var token = await sessions.FindTokenAsync(hash, cancellationToken);
        if (token is null || token.RevokedAt is not null || token.Session.RevokedAt is not null || token.Session.AbsoluteExpiresAt <= now)
            return SessionErrors.RefreshInvalid;

        if (token.ConsumedAt is not { } consumedAt)
            return SessionErrors.RefreshInvalid;   // expired, never used

        if (now - consumedAt <= SessionPolicy.RotationGrace)
            return SessionErrors.RefreshRaced;     // a concurrent rotation just won: retry with the successor

        // replay of a consumed token: someone holds a copy. End the session for everyone holding its tokens.
        var revoked = await RevokeAsync(token.Session.UserId, token.SessionId, null, SessionPolicy.RevokedReason.RefreshReuse, cancellationToken);
        if (revoked.Count > 0)
            await audit.WriteAsync(AuditEvent.Denied(AuditActions.RefreshReuseDetected)
                .On(AuditTargets.Session, token.SessionId.ToString())
                .With(("reason", SessionPolicy.RevokedReason.RefreshReuse)) with { Anonymous = true }, CancellationToken.None);
        return SessionErrors.RefreshInvalid;
    }

    private async Task<IssuedRefreshToken> InsertTokenAsync(UserSession session, long? parentId, DateTime now, CancellationToken cancellationToken)
    {
        var user = (await users.FindByIdAsync(session.UserId))!;
        var sliding = now + SessionPolicy.Lifetime(user.AccountType).Sliding;
        var expiresAt = sliding < session.AbsoluteExpiresAt ? sliding : session.AbsoluteExpiresAt;
        var token = NewToken();
        await sessions.AddRefreshTokenAsync(new SessionRefreshToken
        {
            SessionId = session.Id,
            ParentId = parentId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = expiresAt
        }, cancellationToken);
        return new IssuedRefreshToken(token, expiresAt);
    }

    private sealed record CachedCheck(bool Active, DateTime AbsoluteExpiresAt, DateTime CheckedAt);

    private static string CacheKey(Guid sessionId, string userId) => $"wj:session:{sessionId:N}:{userId}";

    public async Task<bool> IsActiveAsync(Guid sessionId, string userId, CancellationToken cancellationToken)
    {
        var now = Now;
        var key = CacheKey(sessionId, userId);
        if (cache.TryGetValue(key, out CachedCheck? cached) && cached is not null && now - cached.CheckedAt <= SessionPolicy.CheckCacheWindow)
            return cached.Active && cached.AbsoluteExpiresAt > now;

        var session = await sessions.FindActiveSessionAsync(sessionId, userId, now, cancellationToken);
        cache.Set(key, new CachedCheck(session is not null, session?.AbsoluteExpiresAt ?? default, now), SessionPolicy.CheckCacheWindow);
        return session is not null;
    }

    public async Task<IReadOnlyList<UserSession>> ListActiveAsync(string userId, CancellationToken cancellationToken) =>
        await sessions.GetActiveSessionsAsync(userId, Now, cancellationToken);

    public async Task<IReadOnlyList<Guid>> RevokeAsync(string userId, Guid? sessionId, Guid? except, string reason, CancellationToken cancellationToken)
    {
        var revoked = await sessions.RevokeAsync(userId, sessionId, except, reason, Now, cancellationToken);
        foreach (var id in revoked)
            cache.Remove(CacheKey(id, userId));   // effective immediately in this process
        return revoked;
    }
}
