using Microsoft.EntityFrameworkCore;

namespace WazzufJobs.DAL.Persistence;

public static class TransactionExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction that commits only when <paramref name="commit"/> returns
    /// true (rolled back otherwise, or on any exception). Nested calls join the caller's transaction, which then decides
    /// (Phase 02, S5: an audited change and the locks it takes form one transaction). Non-relational stores (tests) run
    /// the work directly.
    /// </summary>
    public static async Task<TResult> RunInTransactionAsync<TResult>(
        this ApplicationDBContext context, Func<Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational() || context.Database.CurrentTransaction is not null)
            return await work();

        // the context uses a retrying execution strategy, so the transaction must run inside it
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var result = await work();
            if (commit(result))
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);
            return result;
        });
    }
}
