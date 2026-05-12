using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class JobRepository(ApplicationDBContext context) : IJobRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<(IEnumerable<Job> Items, int TotalCount)> GetAllAsync(JobFilterRequestDTO filter,CancellationToken cancellationToken)
    {
        var query = _context.Jobs
            .Include(j => j.Category)
            .Include(j => j.PostedBy)
            .AsNoTracking()
            .AsQueryable();

        // filters
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            query = query.Where(j =>
                j.Title.Contains(filter.Keyword) ||
                j.Description.Contains(filter.Keyword));

        if (!string.IsNullOrWhiteSpace(filter.Location))
            query = query.Where(j => j.Location.Contains(filter.Location));

        if (filter.CategoryId.HasValue)
            query = query.Where(j => j.CategoryId == filter.CategoryId);

        if (filter.JobType.HasValue)
            query = query.Where(j => j.JobType == filter.JobType);

        if (filter.WorkplaceType.HasValue)
            query = query.Where(j => j.WorkplaceType == filter.WorkplaceType);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Job?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _context.Jobs
            .Include(j => j.Category)
            .Include(j => j.PostedBy)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<bool> ExistsAsync(string title, CancellationToken cancellationToken) =>
        await _context.Jobs
            .AnyAsync(j => j.Title == title, cancellationToken);

    public async Task AddAsync(Job job, CancellationToken cancellationToken) =>
        await _context.Jobs.AddAsync(job, cancellationToken);

    public Task UpdateAsync(Job job)
    {
        _context.Jobs.Update(job);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Job job)
    {
        _context.Jobs.Remove(job);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}

