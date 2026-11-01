using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Enums;
using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Authentication;

/// <summary>The caller's verified access to one company.</summary>
public sealed record CompanyAccess(Guid CompanyId, string UserId, CompanyMemberRole Role, CompanyStatus CompanyStatus);

/// <summary>
/// Company-level authorization (Phase 02, §4 layer 2). Every company handler calls <see cref="RequireAsync"/> with the
/// <c>companyId</c> from the route before touching company data.
/// </summary>
public interface ICompanyAccessService
{
    /// <summary>
    /// Succeeds only for an active member of the company (see <c>ICompanyRepository.FindActiveMembershipAsync</c>) whose
    /// role grants <paramref name="permission"/>. Otherwise:
    /// no usable membership (also: unknown company, another tenant, removed member, staff) → 404 <c>Company.NotFound</c>;
    /// suspended company (except viewing it) → 403 <c>Company.Suspended</c>; role without the permission → 403
    /// <c>Company.PermissionDenied</c>.
    /// </summary>
    Task<Result<CompanyAccess>> RequireAsync(Guid companyId, CompanyPermission permission, CancellationToken cancellationToken);
}

public sealed class CompanyAccessService(ICurrentUserService currentUser, ICompanyRepository companies) : ICompanyAccessService
{
    public async Task<Result<CompanyAccess>> RequireAsync(Guid companyId, CompanyPermission permission, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CompanyAccess>(CompanyErrors.NotFound);

        var membership = await companies.FindActiveMembershipAsync(companyId, userId, cancellationToken);
        if (membership is null)
            return Result.Failure<CompanyAccess>(CompanyErrors.NotFound);

        if (membership.CompanyStatus == CompanyStatus.Suspended && !CompanyPermissionCatalog.AllowedWhileSuspended.Contains(permission))
            return Result.Failure<CompanyAccess>(CompanyErrors.Suspended);

        if (!CompanyPermissionCatalog.Allows(membership.Role, permission))
            return Result.Failure<CompanyAccess>(CompanyErrors.PermissionDenied);

        return Result.Success(new CompanyAccess(membership.CompanyId, membership.UserId, membership.Role, membership.CompanyStatus));
    }
}
