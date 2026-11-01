using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class AuditRepository(ApplicationDBContext context, DbContextOptions<ApplicationDBContext> options, IDataScope dataScope) : IAuditRepository
{
    public async Task AppendAsync(AuditLog entry, CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendSeparatelyAsync(AuditLog entry, CancellationToken cancellationToken)
    {
        await using var separate = new ApplicationDBContext(options, dataScope);
        separate.AuditLogs.Add(entry);
        await separate.SaveChangesAsync(cancellationToken);
    }

    public Task<TResult> RunInTransactionAsync<TResult>(Func<Task<TResult>> work, Func<TResult, bool> commit, CancellationToken cancellationToken) =>
        context.RunInTransactionAsync(work, commit, cancellationToken);
}
