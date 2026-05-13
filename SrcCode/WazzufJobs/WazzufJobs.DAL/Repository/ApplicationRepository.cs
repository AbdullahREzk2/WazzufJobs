using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;


public class ApplicationRepository(ApplicationDBContext context) : IApplicationRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<(IEnumerable<Application> Items, int TotalCount)> GetByJobIdAsync(int jobId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.Applications
            .Include(a => a.User)
            .ThenInclude(u => u.CV)
            .Where(a => a.JobId == jobId)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<Application> Items, int TotalCount)> GetByUserIdAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.Applications
            .Include(a => a.Job)
            .ThenInclude(j => j.Category)
            .Where(a => a.UserId == userId)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Application?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _context.Applications
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Application?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken) =>
        await _context.Applications
            .Include(a => a.User)
            .ThenInclude(u => u.CV)
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<bool> HasAppliedAsync(string userId, int jobId, CancellationToken cancellationToken) =>
        await _context.Applications
            .AnyAsync(a => a.UserId == userId && a.JobId == jobId, cancellationToken);

    public async Task AddAsync(Application application, CancellationToken cancellationToken) =>
        await _context.Applications.AddAsync(application, cancellationToken);

    public Task UpdateAsync(Application application)
    {
        _context.Applications.Update(application);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);


}
