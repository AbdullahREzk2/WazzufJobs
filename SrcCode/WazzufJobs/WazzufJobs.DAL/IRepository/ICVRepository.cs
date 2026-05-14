using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;

public interface ICVRepository
{
    Task<CV?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task AddAsync(CV cv, CancellationToken cancellationToken);
    Task UpdateAsync(CV cv);
    Task DeleteAsync(CV cv);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
