using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Authorization;

/// <summary>What a company member may do inside their company (Phase 02, §3). Checked by <c>ICompanyAccessService</c>.</summary>
public enum CompanyPermission
{
    ViewCompany,
    ViewMembers,
    EditProfile,
    ManageTeam,
    TransferOwnership,
    DeleteCompany,
    ManageJobs,
    ManageApplicants
}

/// <summary>
/// Company-level permissions per membership role, in code (Phase 02, S4). Membership roles are not platform roles:
/// a company account's rights come only from an active membership in a company.
/// </summary>
public static class CompanyPermissionCatalog
{
    private static readonly IReadOnlyDictionary<CompanyMemberRole, IReadOnlySet<CompanyPermission>> Grants =
        new Dictionary<CompanyMemberRole, IReadOnlySet<CompanyPermission>>
        {
            [CompanyMemberRole.Owner] = Enum.GetValues<CompanyPermission>().ToHashSet(),
            [CompanyMemberRole.Admin] = new HashSet<CompanyPermission>
            {
                CompanyPermission.ViewCompany, CompanyPermission.ViewMembers, CompanyPermission.EditProfile,
                CompanyPermission.ManageTeam, CompanyPermission.ManageJobs, CompanyPermission.ManageApplicants
            },
            [CompanyMemberRole.Recruiter] = new HashSet<CompanyPermission>
            {
                CompanyPermission.ViewCompany, CompanyPermission.ViewMembers,
                CompanyPermission.ManageJobs, CompanyPermission.ManageApplicants
            }
        };

    /// <summary>Permissions a suspended company's members keep: only seeing the company (and its status).</summary>
    public static IReadOnlySet<CompanyPermission> AllowedWhileSuspended { get; } = new HashSet<CompanyPermission> { CompanyPermission.ViewCompany };

    public static IReadOnlySet<CompanyPermission> PermissionsOf(CompanyMemberRole role) =>
        Grants.TryGetValue(role, out var permissions) ? permissions : new HashSet<CompanyPermission>();

    public static bool Allows(CompanyMemberRole role, CompanyPermission permission) => PermissionsOf(role).Contains(permission);
}
