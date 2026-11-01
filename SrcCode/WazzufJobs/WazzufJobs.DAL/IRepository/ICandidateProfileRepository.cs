using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;

/// <summary>Candidate onboarding profile and job preferences (Phase 02 S2b; replaces onboarding columns and UserPreferences).</summary>
public interface ICandidateProfileRepository
{
    Task<CandidateProfile?> GetAsync(string userId, CancellationToken cancellationToken);
    Task AddAsync(CandidateProfile profile, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
