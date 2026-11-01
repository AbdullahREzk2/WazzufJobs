using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.IRepository;

/// <summary>
/// Companies (Phase 02). Every company-module method is scoped by the <c>companyId</c> it receives first (taken from the
/// route by callers, never from a request body): there is no unscoped lookup (enforced by a test). The only exception
/// is the platform company lookup used by job creation.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>Id of the single active platform company, or null when it is missing (callers must fail closed).</summary>
    Task<Guid?> GetPlatformCompanyIdAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The user's active membership in the company, only when it can grant access: the company is visible in the
    /// request's data scope and is not the platform company, and the account is an enabled company account in the
    /// company's partition (real or demo). Otherwise null.
    /// </summary>
    Task<CompanyMembership?> FindActiveMembershipAsync(Guid companyId, string userId, CancellationToken cancellationToken);

    Task<CompanyProfile?> GetProfileAsync(Guid companyId, CancellationToken cancellationToken);

    Task<List<CompanyMemberSummary>> GetActiveMembersAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Role and account state of an active member of this company, or null.</summary>
    Task<CompanyMemberState?> GetActiveMemberAsync(Guid companyId, string userId, CancellationToken cancellationToken);

    /// <summary>Active owners whose accounts are enabled.</summary>
    Task<int> CountActiveOwnersAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Changes the role of an active member of this company. False when there is no such member.</summary>
    Task<bool> SetMemberRoleAsync(Guid companyId, string userId, CompanyMemberRole role, CancellationToken cancellationToken);

    /// <summary>Marks an active member of this company as removed. False when there is no such member.</summary>
    Task<bool> RemoveMemberAsync(Guid companyId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Runs a membership change while holding an exclusive lock on the company row, so concurrent changes to the same
    /// company run one after another and each sees the previous one's result (last-owner guard). Commits only when
    /// <paramref name="succeeded"/> returns true.
    /// </summary>
    Task<TResult> RunExclusiveForCompanyAsync<TResult>(Guid companyId, Func<Task<TResult>> change, Func<TResult, bool> succeeded, CancellationToken cancellationToken);
}
