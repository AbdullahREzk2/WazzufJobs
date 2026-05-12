using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;
public interface IJobRepository
{
    Task<(IEnumerable<Job> Items, int TotalCount)> GetAllAsync(JobFilterRequestDTO filter,CancellationToken cancellationToken);
    Task<Job?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string title, CancellationToken cancellationToken);
    Task AddAsync(Job job, CancellationToken cancellationToken);
    Task UpdateAsync(Job job);
    Task DeleteAsync(Job job);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
