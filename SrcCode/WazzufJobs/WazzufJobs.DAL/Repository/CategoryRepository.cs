using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class CategoryRepository(ApplicationDBContext context) : ICategoryRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<IEnumerable<JobCategory>> GetAllAsync(CancellationToken cancellationToken) =>
        await _context.JobCategories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<JobCategory?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _context.JobCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<JobCategory?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        await _context.JobCategories
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) =>
        await _context.JobCategories
            .AnyAsync(c => c.Name == name, cancellationToken);

    public async Task AddAsync(JobCategory category, CancellationToken cancellationToken) =>
        await _context.JobCategories.AddAsync(category, cancellationToken);

    public Task UpdateAsync(JobCategory category)
    {
        _context.JobCategories.Update(category);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(JobCategory category)
    {
        _context.JobCategories.Remove(category);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);

}

