using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class SessionRepository(ApplicationDBContext context) : ISessionRepository
{
    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        context.UserSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRefreshTokenAsync(SessionRefreshToken token, CancellationToken cancellationToken)
    {
        context.SessionRefreshTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SessionRefreshToken?> TryConsumeAsync(byte[] tokenHash, DateTime now, CancellationToken cancellationToken)
    {
        var usable = context.SessionRefreshTokens.Where(t =>
            t.TokenHash == tokenHash && t.ConsumedAt == null && t.RevokedAt == null && t.ExpiresAt > now);

        if (context.Database.IsRelational())
        {
            // UPDATE ... WHERE <usable>: the predicate is re-evaluated on the latest committed row under an exclusive
            // lock (also with READ_COMMITTED_SNAPSHOT), so exactly one concurrent caller updates the row
            var updated = await usable.ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), cancellationToken);
            return updated == 1 ? await FindTokenAsync(tokenHash, cancellationToken) : null;
        }

        // non-relational test store: no concurrency guarantee needed
        var token = await usable.SingleOrDefaultAsync(cancellationToken);
        if (token is null)
            return null;
        token.ConsumedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<bool> TouchActiveSessionAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken)
    {
        var active = context.UserSessions.Where(s =>
            s.Id == sessionId && s.RevokedAt == null && s.AbsoluteExpiresAt > now && !s.User.IsDisabled);

        if (context.Database.IsRelational())
            return await active.ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now), cancellationToken) == 1;

        var session = await active.SingleOrDefaultAsync(cancellationToken);
        if (session is null)
            return false;
        session.LastSeenAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SessionRefreshToken?> FindTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        await context.SessionRefreshTokens.AsNoTracking()
            .Include(t => t.Session)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<UserSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await context.UserSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task<UserSession?> FindActiveSessionAsync(Guid sessionId, string userId, DateTime now, CancellationToken cancellationToken) =>
        await context.UserSessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null
                                       && s.AbsoluteExpiresAt > now && !s.User.IsDisabled, cancellationToken);

    public async Task<List<UserSession>> GetActiveSessionsAsync(string userId, DateTime now, CancellationToken cancellationToken) =>
        await context.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.AbsoluteExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(cancellationToken);

    public async Task<List<Guid>> RevokeAsync(
        string userId, Guid? sessionId, Guid? except, string reason, DateTime now, CancellationToken cancellationToken)
    {
        var targets = context.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null
            && (sessionId == null || s.Id == sessionId) && (except == null || s.Id != except));

        if (!context.Database.IsRelational())
        {
            var sessions = await targets.Include(s => s.RefreshTokens).ToListAsync(cancellationToken);
            foreach (var session in sessions)
            {
                session.RevokedAt = now;
                session.RevokedReason = reason;
                foreach (var token in session.RefreshTokens.Where(t => t.ConsumedAt == null && t.RevokedAt == null))
                    token.RevokedAt = now;
            }
            await context.SaveChangesAsync(cancellationToken);
            return sessions.Select(s => s.Id).ToList();
        }

        return await context.RunInTransactionAsync(async () =>
        {
            // compare-and-swap per session: only sessions still unrevoked at update time are counted as revoked here
            var candidates = await targets.Select(s => s.Id).ToListAsync(cancellationToken);
            var revoked = new List<Guid>();
            foreach (var id in candidates)
            {
                var updated = await context.UserSessions
                    .Where(s => s.Id == id && s.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedReason, reason), cancellationToken);
                if (updated == 1)
                    revoked.Add(id);
            }

            if (revoked.Count > 0)
                await context.SessionRefreshTokens
                    .Where(t => revoked.Contains(t.SessionId) && t.ConsumedAt == null && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
            return revoked;
        }, _ => true, cancellationToken);
    }

    public Task<TResult> RunInTransactionAsync<TResult>(Func<Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken) =>
        context.RunInTransactionAsync(work, commit, cancellationToken);
}
