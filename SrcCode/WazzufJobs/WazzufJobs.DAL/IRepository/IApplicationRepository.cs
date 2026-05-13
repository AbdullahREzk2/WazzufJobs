using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;
public interface IApplicationRepository
{
    Task<(IEnumerable<Application> Items, int TotalCount)> GetByJobIdAsync(int jobId, int page, int pageSize, CancellationToken cancellationToken);
    Task<(IEnumerable<Application> Items, int TotalCount)> GetByUserIdAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Application?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Application?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken);
    Task<bool> HasAppliedAsync(string userId, int jobId, CancellationToken cancellationToken);
    Task AddAsync(Application application, CancellationToken cancellationToken);
    Task UpdateAsync(Application application);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
