namespace WazzufJobs.DAL.IRepository;

/// <summary>Companies (Phase 02). S2b needs only the platform company; company management arrives in S4 and P04.</summary>
public interface ICompanyRepository
{
    /// <summary>Id of the single active platform company, or null when it is missing (callers must fail closed).</summary>
    Task<Guid?> GetPlatformCompanyIdAsync(CancellationToken cancellationToken);
}
