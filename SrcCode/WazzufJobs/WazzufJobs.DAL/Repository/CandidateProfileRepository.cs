using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class CandidateProfileRepository(ApplicationDBContext context) : ICandidateProfileRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<CandidateProfile?> GetAsync(string userId, CancellationToken cancellationToken) =>
        await _context.CandidateProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task AddAsync(CandidateProfile profile, CancellationToken cancellationToken) =>
        await _context.CandidateProfiles.AddAsync(profile, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}
