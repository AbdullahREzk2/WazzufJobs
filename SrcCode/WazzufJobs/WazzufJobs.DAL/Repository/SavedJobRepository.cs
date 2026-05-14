using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class SavedJobRepository(ApplicationDBContext context) : ISavedJobRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<IEnumerable<SavedJob>> GetByUserIdAsync(string userId,CancellationToken cancellationToken) =>
        await _context.SavedJobs
            .Include(s => s.Job)
                .ThenInclude(j => j.Category)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SavedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<SavedJob?> GetAsync(string userId,int jobId,CancellationToken cancellationToken) =>
        await _context.SavedJobs
            .FirstOrDefaultAsync(
                s => s.UserId == userId && s.JobId == jobId,
                cancellationToken);

    public async Task<bool> IsSavedAsync(string userId,int jobId,CancellationToken cancellationToken) =>
        await _context.SavedJobs
            .AnyAsync(
                s => s.UserId == userId && s.JobId == jobId,
                cancellationToken);

    public async Task AddAsync(SavedJob savedJob, CancellationToken cancellationToken) =>
        await _context.SavedJobs.AddAsync(savedJob, cancellationToken);

    public Task DeleteAsync(SavedJob savedJob)
    {
        _context.SavedJobs.Remove(savedJob);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}