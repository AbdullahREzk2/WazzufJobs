using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Authorization;

/// <summary>
/// Permission strings carried in the access token's <c>permissions</c> claim and checked by <c>[HasPermission]</c>.
/// The strings are an API contract: append-only, never renamed or reused.
/// </summary>
public static class Permissions
{
    public const string Type = "permissions";

    // Jobs
    public const string JobsRead = "jobs:read";
    public const string JobsCreate = "jobs:create";
    public const string JobsUpdate = "jobs:update";
    public const string JobsDelete = "jobs:delete";

    // Applications
    public const string ApplicationsRead = "applications:read";
    public const string ApplicationsCreate = "applications:create";
    public const string ApplicationsUpdate = "applications:update";

    // Categories
    public const string CategoriesRead = "categories:read";
    public const string CategoriesCreate = "categories:create";
    public const string CategoriesUpdate = "categories:update";
    public const string CategoriesDelete = "categories:delete";

    // Users
    public const string UsersRead = "users:read";
    public const string UsersUpdate = "users:update";
    public const string UsersDelete = "users:delete";
    public const string UsersCreate = "users:create";

    // Roles (roles are fixed since Phase 02 S3: only roles:read is granted)
    public const string RolesRead = "roles:read";
    public const string RolesCreate = "roles:create";
    public const string RolesUpdate = "roles:update";
    public const string RolesDelete = "roles:delete";

    // CV
    public const string CVUpload = "cv:upload";
    public const string CVDelete = "cv:delete";

    // SavedJobs
    public const string SavedJobsRead = "savedjobs:read";
    public const string SavedJobsCreate = "savedjobs:create";
    public const string SavedJobsDelete = "savedjobs:delete";

    /// <summary>Every defined permission string, in catalog order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        JobsRead, JobsCreate, JobsUpdate, JobsDelete,
        ApplicationsRead, ApplicationsCreate, ApplicationsUpdate,
        CategoriesRead, CategoriesCreate, CategoriesUpdate, CategoriesDelete,
        UsersRead, UsersCreate, UsersUpdate, UsersDelete,
        RolesRead, RolesCreate, RolesUpdate, RolesDelete,
        CVUpload, CVDelete,
        SavedJobsRead, SavedJobsCreate, SavedJobsDelete
    ];

    /// <summary>Kept for compatibility (existing clients know them) but granted to no role: roles cannot be changed.</summary>
    public static IReadOnlyList<string> Retired { get; } = [RolesCreate, RolesUpdate, RolesDelete];
}

/// <summary>The fixed platform role names (Phase 02, architecture §2).</summary>
public static class PlatformRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Moderator = "Moderator";
    public const string Candidate = "Candidate";
    public const string CompanyUser = "CompanyUser";
    public const string DemoAdmin = "DemoAdmin";
}

/// <summary>
/// Legacy role names that the existing Angular guards read from the token's <c>roles</c> claim. They are added to the
/// token for display compatibility only: they are not roles, the API never authorizes by role, and they are never
/// accepted as input.
/// </summary>
public static class LegacyRoleAliases
{
    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>A fixed platform role: identity, the account type it belongs to and its permissions.</summary>
public sealed record PlatformRole(
    string Name,
    string Id,
    string ConcurrencyStamp,
    AccountType AccountType,
    bool DemoOnly,
    bool IsStaff,
    string? LegacyAlias,
    string Description,
    IReadOnlyList<string> Permissions)
{
    /// <summary>Whether an account of this type (and demo flag) may hold the role.</summary>
    public bool IsCompatibleWith(AccountType accountType, bool isDemo) =>
        accountType == AccountType && (!DemoOnly || isDemo) && !(IsStaff && !DemoOnly && isDemo);
}

/// <summary>What an account may do: its effective roles, the token's role claim values and its permissions.</summary>
public sealed record AccessGrant(
    AccountType AccountType,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> TokenRoles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> IgnoredRoles);

public enum RoleAssignmentError
{
    None,
    UnknownRole,
    IncompatibleWithAccount
}

/// <summary>
/// The single source of truth for platform roles and their permissions (Phase 02, S3). Role rows and role claims are
/// seeded from it (<c>RoleConfiguration</c>, <c>RoleClaimConfiguration</c>), access tokens are built from it, and the
/// Roles API reads it. A test asserts that the database matches it.
/// </summary>
public static class PermissionCatalog
{
    public static readonly PlatformRole SuperAdmin = new(
        PlatformRoles.SuperAdmin,
        Id: "bf766c90-9e7c-4798-99c6-e90fe3a7960f",
        ConcurrencyStamp: "17291d98-8355-4440-a359-d4138485b8e6",
        AccountType.Staff, DemoOnly: false, IsStaff: true, LegacyAlias: LegacyRoleAliases.Admin,
        "Platform owner: every platform permission, including users, roles (read-only) and applications.",
        Permissions.All.Except(Permissions.Retired).ToList());

    public static readonly PlatformRole Moderator = new(
        PlatformRoles.Moderator,
        Id: "21eb5d41-fffb-4b7d-8c09-abb15d40187b",
        ConcurrencyStamp: "0de9d4f3-4fb2-4bac-bf7e-f5cd60efc2b0",
        AccountType.Staff, DemoOnly: false, IsStaff: true, LegacyAlias: null,
        "Platform moderation and the user list (read-only). Never CVs, applications or AI assessments.",
        [Permissions.JobsRead, Permissions.CategoriesRead, Permissions.UsersRead]);

    public static readonly PlatformRole Candidate = new(
        PlatformRoles.Candidate,
        Id: "eb935101-a57b-48a7-ad2d-ee21fc5e6ad1",
        ConcurrencyStamp: "304deecb-d38e-43eb-aad3-b0476d91b2a3",
        AccountType.Candidate, DemoOnly: false, IsStaff: false, LegacyAlias: LegacyRoleAliases.User,
        "Job seeker: own profile, CV, saved jobs and applications.",
        [
            Permissions.JobsRead,
            Permissions.ApplicationsRead, Permissions.ApplicationsCreate,
            Permissions.CategoriesRead,
            Permissions.CVUpload, Permissions.CVDelete,
            Permissions.SavedJobsRead, Permissions.SavedJobsCreate, Permissions.SavedJobsDelete
        ]);

    public static readonly PlatformRole CompanyUser = new(
        PlatformRoles.CompanyUser,
        Id: "f6d38004-b93a-4e15-80c0-824e87e50339",
        ConcurrencyStamp: "7dcedc97-bb94-4df4-9744-3fa4b6870d12",
        AccountType.Company, DemoOnly: false, IsStaff: false, LegacyAlias: null,
        "Company account: no platform permissions; rights come from company membership.",
        []);

    public static readonly PlatformRole DemoAdmin = new(
        PlatformRoles.DemoAdmin,
        Id: "6b2d257a-0e04-49aa-9d22-e893759ed0c9",
        ConcurrencyStamp: "cb1d55b7-5d07-4845-9c1f-fba303e9f9c4",
        AccountType.Staff, DemoOnly: true, IsStaff: true, LegacyAlias: null,
        "Demo staff: public reference data only until demo scope exists. Never real users, CVs or AI assessments.",
        [Permissions.JobsRead, Permissions.CategoriesRead]);

    /// <summary>The fixed platform roles, in catalog order. Roles cannot be created, changed or deleted.</summary>
    public static IReadOnlyList<PlatformRole> Roles { get; } = [SuperAdmin, Moderator, Candidate, CompanyUser, DemoAdmin];

    public static PlatformRole? Find(string? name) =>
        Roles.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

    public static PlatformRole? FindById(string? id) =>
        Roles.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool IsStaffRole(string? name) => Find(name)?.IsStaff == true;

    /// <summary>
    /// Checks a role set for an account: every name must be a platform role (legacy aliases are not), and the account
    /// may hold at most one role, compatible with its type and demo flag.
    /// </summary>
    public static RoleAssignmentError ValidateAssignment(AccountType accountType, bool isDemo, IEnumerable<string> roles)
    {
        var requested = roles.ToList();
        if (requested.Any(r => Find(r) is null))
            return RoleAssignmentError.UnknownRole;

        var distinct = requested.Select(r => Find(r)!).Distinct().ToList();
        if (distinct.Count > 1 || distinct.Any(r => !r.IsCompatibleWith(accountType, isDemo)))
            return RoleAssignmentError.IncompatibleWithAccount;

        return RoleAssignmentError.None;
    }

    /// <summary>
    /// The access an account actually receives. Only platform roles compatible with the account type count: unknown
    /// names (including the legacy "Admin" and "User") and incompatible roles grant nothing. The token's role values
    /// are the effective roles plus their legacy aliases; permissions come from this catalog, never from the aliases.
    /// </summary>
    public static AccessGrant Resolve(AccountType accountType, bool isDemo, IEnumerable<string> assignedRoles)
    {
        var assigned = assignedRoles.ToList();
        var effective = Roles
            .Where(r => assigned.Contains(r.Name, StringComparer.OrdinalIgnoreCase) && r.IsCompatibleWith(accountType, isDemo))
            .ToList();

        var ignored = assigned
            .Where(name => !effective.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var names = effective.Select(r => r.Name).ToList();
        var tokenRoles = names.Concat(effective.Where(r => r.LegacyAlias is not null).Select(r => r.LegacyAlias!)).ToList();
        var permissions = effective.SelectMany(r => r.Permissions).Distinct().ToList();

        return new AccessGrant(accountType, names, tokenRoles, permissions, ignored);
    }
}
