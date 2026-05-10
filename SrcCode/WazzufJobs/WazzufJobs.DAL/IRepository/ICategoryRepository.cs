using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;
public interface ICategoryRepository
{
    Task<IEnumerable<JobCategory>> GetAllAsync(CancellationToken cancellationToken);
    Task<JobCategory?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<JobCategory?> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken);
    Task AddAsync(JobCategory category, CancellationToken cancellationToken);
    Task UpdateAsync(JobCategory category);
    Task DeleteAsync(JobCategory category);
    Task SaveChangesAsync(CancellationToken cancellationToken);

}
