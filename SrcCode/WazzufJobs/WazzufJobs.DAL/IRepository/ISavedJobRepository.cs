using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;

public interface ISavedJobRepository
{
    Task<IEnumerable<SavedJob>> GetByUserIdAsync(string userId,CancellationToken cancellationToken);
    Task<SavedJob?> GetAsync(string userId,int jobId,CancellationToken cancellationToken);
    Task<bool> IsSavedAsync(string userId,int jobId,CancellationToken cancellationToken);
    Task AddAsync(SavedJob savedJob, CancellationToken cancellationToken);
    Task DeleteAsync(SavedJob savedJob);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}