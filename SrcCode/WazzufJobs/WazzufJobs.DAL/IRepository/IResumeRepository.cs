using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.IRepository;

/// <summary>
/// Candidate resumes (Phase 02 S2b; replaces the CVs table). Until resume versioning (P05) a candidate has one
/// current resume: the default, active one.
/// </summary>
public interface IResumeRepository
{
    /// <summary>The candidate's current resume (default and active), with its extracted text when requested.</summary>
    Task<Resume?> GetCurrentAsync(string candidateId, bool includeText, CancellationToken cancellationToken);

    Task<bool> HasCurrentAsync(string candidateId, CancellationToken cancellationToken);

    /// <summary>Whether any application references the resume (it then cannot be deleted).</summary>
    Task<bool> IsReferencedAsync(int resumeId, CancellationToken cancellationToken);

    Task AddAsync(Resume resume, CancellationToken cancellationToken);
    void Remove(Resume resume);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
