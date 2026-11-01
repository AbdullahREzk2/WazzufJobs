using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class ResumeRepository(ApplicationDBContext context) : IResumeRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<Resume?> GetCurrentAsync(string candidateId, bool includeText, CancellationToken cancellationToken)
    {
        var query = _context.Resumes.Where(r => r.CandidateId == candidateId && r.IsDefault && r.Status == ResumeStatus.Active);
        if (includeText)
            query = query.Include(r => r.Text);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasCurrentAsync(string candidateId, CancellationToken cancellationToken) =>
        await _context.Resumes.AnyAsync(r => r.CandidateId == candidateId && r.IsDefault && r.Status == ResumeStatus.Active, cancellationToken);

    public async Task<bool> IsReferencedAsync(int resumeId, CancellationToken cancellationToken) =>
        await _context.Applications.AnyAsync(a => a.ResumeId == resumeId, cancellationToken);

    public async Task AddAsync(Resume resume, CancellationToken cancellationToken) =>
        await _context.Resumes.AddAsync(resume, cancellationToken);

    public void Remove(Resume resume) => _context.Resumes.Remove(resume);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}
