using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;

/// <summary>Sessions and their hashed refresh tokens (Phase 02, S6a; architecture §5). Times are passed in (testable clock).</summary>
public interface ISessionRepository
{
    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);

    /// <summary>Inserts a refresh token. The filtered unique index rejects a second usable token for the session.</summary>
    Task AddRefreshTokenAsync(SessionRefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Compare-and-swap consumption: marks the token consumed only if it is unused, unrevoked and unexpired. Concurrent
    /// callers serialize on the row lock; exactly one gets the token, the others null.
    /// </summary>
    Task<SessionRefreshToken?> TryConsumeAsync(byte[] tokenHash, DateTime now, CancellationToken cancellationToken);

    /// <summary>Records activity on a session that is unrevoked, unexpired and whose account is enabled; false otherwise.</summary>
    Task<bool> TouchActiveSessionAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken);

    /// <summary>A token with its session (read-only), or null.</summary>
    Task<SessionRefreshToken?> FindTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>The session if it belongs to the user, is unrevoked and unexpired, and the account is enabled.</summary>
    Task<UserSession?> FindActiveSessionAsync(Guid sessionId, string userId, DateTime now, CancellationToken cancellationToken);

    Task<List<UserSession>> GetActiveSessionsAsync(string userId, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes sessions (compare-and-swap on <c>RevokedAt IS NULL</c>) and their usable refresh tokens. With
    /// <paramref name="sessionId"/> only that session of the user; otherwise every session of the user except
    /// <paramref name="except"/>. Returns the ids actually revoked by this call.
    /// </summary>
    Task<List<Guid>> RevokeAsync(string userId, Guid? sessionId, Guid? except, string reason, DateTime now, CancellationToken cancellationToken);

    Task<TResult> RunInTransactionAsync<TResult>(Func<Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken);
}
