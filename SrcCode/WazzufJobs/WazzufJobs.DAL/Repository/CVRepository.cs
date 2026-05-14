using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class CVRepository(ApplicationDBContext context) : ICVRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<CV?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        await _context.CVs
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task AddAsync(CV cv, CancellationToken cancellationToken) =>
        await _context.CVs.AddAsync(cv, cancellationToken);

    public Task UpdateAsync(CV cv)
    {
        _context.CVs.Update(cv);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CV cv)
    {
        _context.CVs.Remove(cv);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);

}