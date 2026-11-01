using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;

/// <summary>
/// Append-only access to <c>AuditLogs</c> (Phase 02, S5). There is deliberately no read, update or delete method: the
/// table is append-only (database trigger) and audit viewing arrives with the Super Admin console.
/// </summary>
public interface IAuditRepository
{
    /// <summary>Appends on the request's context, inside its current transaction: saved together with the change.</summary>
    Task AppendAsync(AuditLog entry, CancellationToken cancellationToken);

    /// <summary>
    /// Appends through a separate context and connection: never flushes the request's pending changes and is not undone
    /// when the request's transaction rolls back (sign-in events, failures and denials).
    /// </summary>
    Task AppendSeparatelyAsync(AuditLog entry, CancellationToken cancellationToken);

    /// <summary>Runs work in one transaction on the request's context (see <c>RunInTransactionAsync</c>).</summary>
    Task<TResult> RunInTransactionAsync<TResult>(Func<Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken);
}
