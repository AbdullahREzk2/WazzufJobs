using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// The fixed platform roles (Phase 02, S3). Expected values are written out here by hand, independently of
/// <see cref="PermissionCatalog"/>, so a change to the catalog must also change this reviewed specification.
/// </summary>
public class RoleCatalogTests
{
    // the C0 permission sets, unchanged strings (API contract)
    private static readonly string[] LegacyUserPermissions =
        ["jobs:read", "applications:read", "applications:create", "categories:read", "cv:upload", "cv:delete", "savedjobs:read", "savedjobs:create", "savedjobs:delete"];

    private static readonly string[] AllPermissionStrings =
    [
        "jobs:read", "jobs:create", "jobs:update", "jobs:delete",
        "applications:read", "applications:create", "applications:update",
        "categories:read", "categories:create", "categories:update", "categories:delete",
        "users:read", "users:create", "users:update", "users:delete",
        "roles:read", "roles:create", "roles:update", "roles:delete",
        "cv:upload", "cv:delete",
        "savedjobs:read", "savedjobs:create", "savedjobs:delete"
    ];

    public static TheoryData<string, AccountType, bool, string[]> Specification => new()
    {
        { "SuperAdmin", AccountType.Staff, false, AllPermissionStrings.Except(["roles:create", "roles:update", "roles:delete"]).ToArray() },
        { "Moderator", AccountType.Staff, false, ["jobs:read", "categories:read", "users:read"] },
        { "Candidate", AccountType.Candidate, false, LegacyUserPermissions },
        { "CompanyUser", AccountType.Company, false, [] },
        { "DemoAdmin", AccountType.Staff, true, ["jobs:read", "categories:read"] }
    };

    [Fact]
    public void Exactly_the_five_fixed_platform_roles_exist()
    {
        Assert.Equal(["SuperAdmin", "Moderator", "Candidate", "CompanyUser", "DemoAdmin"], PermissionCatalog.Roles.Select(r => r.Name));
        Assert.Equal(5, PermissionCatalog.Roles.Select(r => r.Id.ToUpperInvariant()).Distinct().Count());
        Assert.Null(PermissionCatalog.Find("Admin"));
        Assert.Null(PermissionCatalog.Find("User"));
    }

    [Theory]
    [MemberData(nameof(Specification))]
    public void Each_role_has_exactly_its_specified_account_type_and_permissions(string name, AccountType accountType, bool demoOnly, string[] permissions)
    {
        var role = PermissionCatalog.Find(name)!;

        Assert.Equal(accountType, role.AccountType);
        Assert.Equal(demoOnly, role.DemoOnly);
        Assert.Equal(accountType == AccountType.Staff, role.IsStaff);
        Assert.Equal(permissions.Order(), role.Permissions.Order());
        Assert.Equal(role.Permissions.Count, role.Permissions.Distinct().Count());
    }

    [Fact]
    public void Existing_permission_strings_are_preserved_and_retired_ones_are_granted_to_no_role()
    {
        Assert.Equal(AllPermissionStrings, Permissions.All);
        Assert.Equal(["roles:create", "roles:update", "roles:delete"], Permissions.Retired);
        Assert.All(PermissionCatalog.Roles, r => Assert.Empty(r.Permissions.Intersect(Permissions.Retired)));
        Assert.All(PermissionCatalog.Roles, r => Assert.Empty(r.Permissions.Except(Permissions.All)));
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("DemoAdmin")]
    public void Moderators_and_demo_admins_get_no_cv_application_ai_or_privileged_setting_permission(string name)
    {
        string[] privileged =
        [
            "applications:update",                                                   // admin applications, CV download, AI scores
            "applications:read", "applications:create", "cv:upload", "cv:delete",    // candidate data
            "users:create", "users:update", "users:delete",
            "roles:read", "roles:create", "roles:update", "roles:delete",
            "categories:create", "categories:update", "categories:delete",
            "jobs:create", "jobs:update", "jobs:delete"
        ];

        Assert.Empty(PermissionCatalog.Find(name)!.Permissions.Intersect(privileged));
    }

    [Fact]
    public void Demo_admins_cannot_list_real_users_until_demo_scope_exists()
    {
        // identity tables are not demo-filtered (S9), so users:read would show real accounts to shared demo credentials
        Assert.DoesNotContain("users:read", PermissionCatalog.DemoAdmin.Permissions);
    }

    [Theory]
    [InlineData("SuperAdmin", AccountType.Staff, false, true)]
    [InlineData("SuperAdmin", AccountType.Staff, true, false)]
    [InlineData("SuperAdmin", AccountType.Candidate, false, false)]
    [InlineData("SuperAdmin", AccountType.Company, false, false)]
    [InlineData("Moderator", AccountType.Staff, false, true)]
    [InlineData("Moderator", AccountType.Staff, true, false)]
    [InlineData("Moderator", AccountType.Candidate, false, false)]
    [InlineData("DemoAdmin", AccountType.Staff, true, true)]
    [InlineData("DemoAdmin", AccountType.Staff, false, false)]
    [InlineData("DemoAdmin", AccountType.Candidate, true, false)]
    [InlineData("Candidate", AccountType.Candidate, false, true)]
    [InlineData("Candidate", AccountType.Candidate, true, true)]
    [InlineData("Candidate", AccountType.Staff, false, false)]
    [InlineData("Candidate", AccountType.Company, false, false)]
    [InlineData("CompanyUser", AccountType.Company, false, true)]
    [InlineData("CompanyUser", AccountType.Company, true, true)]
    [InlineData("CompanyUser", AccountType.Candidate, false, false)]
    [InlineData("CompanyUser", AccountType.Staff, false, false)]
    public void Roles_are_compatible_only_with_their_account_type(string role, AccountType accountType, bool isDemo, bool compatible)
    {
        Assert.Equal(compatible, PermissionCatalog.Find(role)!.IsCompatibleWith(accountType, isDemo));
        Assert.Equal(compatible ? RoleAssignmentError.None : RoleAssignmentError.IncompatibleWithAccount,
            PermissionCatalog.ValidateAssignment(accountType, isDemo, [role]));
    }

    [Theory]
    [InlineData(AccountType.Candidate, false, new string[0], RoleAssignmentError.None)]
    [InlineData(AccountType.Candidate, false, new[] { "candidate" }, RoleAssignmentError.None)]
    [InlineData(AccountType.Candidate, false, new[] { "User" }, RoleAssignmentError.UnknownRole)]
    [InlineData(AccountType.Staff, false, new[] { "Admin" }, RoleAssignmentError.UnknownRole)]
    [InlineData(AccountType.Staff, false, new[] { "SuperAdmin", "Moderator" }, RoleAssignmentError.IncompatibleWithAccount)]
    [InlineData(AccountType.Candidate, false, new[] { "Candidate", "CompanyUser" }, RoleAssignmentError.IncompatibleWithAccount)]
    public void Assignments_allow_at_most_one_known_matching_role(AccountType accountType, bool isDemo, string[] roles, RoleAssignmentError expected)
    {
        Assert.Equal(expected, PermissionCatalog.ValidateAssignment(accountType, isDemo, roles));
    }

    [Theory]
    [InlineData("SuperAdmin", AccountType.Staff, new[] { "SuperAdmin", "Admin" })]
    [InlineData("Candidate", AccountType.Candidate, new[] { "Candidate", "User" })]
    [InlineData("Moderator", AccountType.Staff, new[] { "Moderator" })]
    [InlineData("CompanyUser", AccountType.Company, new[] { "CompanyUser" })]
    public void Token_roles_add_the_legacy_alias_only_to_super_admins_and_candidates(string role, AccountType accountType, string[] tokenRoles)
    {
        var grant = PermissionCatalog.Resolve(accountType, false, [role]);

        Assert.Equal(tokenRoles, grant.TokenRoles);
        Assert.Equal(PermissionCatalog.Find(role)!.Permissions.Order(), grant.Permissions.Order());
    }

    [Theory]
    [InlineData(AccountType.Staff, false, new[] { "Admin" })]                 // legacy alias stored as a role
    [InlineData(AccountType.Candidate, false, new[] { "User" })]
    [InlineData(AccountType.Candidate, false, new[] { "SuperAdmin" })]         // staff role on a candidate account
    [InlineData(AccountType.Company, false, new[] { "Moderator" })]
    [InlineData(AccountType.Staff, true, new[] { "SuperAdmin" })]              // real staff role on a demo account
    [InlineData(AccountType.Staff, false, new[] { "DemoAdmin" })]              // demo role on a real account
    [InlineData(AccountType.Staff, false, new[] { "Candidate", "Owner", "" })]
    public void Unknown_and_incompatible_roles_grant_nothing(AccountType accountType, bool isDemo, string[] assigned)
    {
        var grant = PermissionCatalog.Resolve(accountType, isDemo, assigned);

        Assert.Empty(grant.Roles);
        Assert.Empty(grant.TokenRoles);
        Assert.Empty(grant.Permissions);
        Assert.Equal(assigned, grant.IgnoredRoles);
    }

    [Fact]
    public void A_compatible_role_keeps_its_grant_when_an_incompatible_one_is_also_assigned()
    {
        var grant = PermissionCatalog.Resolve(AccountType.Candidate, false, ["Candidate", "SuperAdmin", "Admin"]);

        Assert.Equal(["Candidate"], grant.Roles);
        Assert.Equal(["Candidate", "User"], grant.TokenRoles);
        Assert.Equal(LegacyUserPermissions.Order(), grant.Permissions.Order());
        Assert.Equal(["SuperAdmin", "Admin"], grant.IgnoredRoles);
    }
}

/// <summary>The database seed equals the catalog (Baseline_v2 on a throwaway SQL Server database).</summary>
public class RoleCatalogSeedTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [SqlServerFact]
    public async Task Seeded_roles_and_permission_claims_equal_the_catalog()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        var roles = await db.Roles.AsNoTracking().ToListAsync();
        Assert.Equal(
            PermissionCatalog.Roles.Select(r => (r.Id, r.Name, r.Name.ToUpperInvariant(), r.ConcurrencyStamp, true, r.Description)).OrderBy(r => r.Id),
            roles.Select(r => (r.Id, r.Name!, r.NormalizedName!, r.ConcurrencyStamp!, r.IsSystem, r.Description!)).OrderBy(r => r.Id));

        var claims = await db.RoleClaims.AsNoTracking().Select(c => new { c.RoleId, c.ClaimType, c.ClaimValue }).ToListAsync();
        Assert.Equal(
            PermissionCatalog.Roles.SelectMany(r => r.Permissions, (r, p) => $"{r.Id}|{Permissions.Type}|{p}").Order(),
            claims.Select(c => $"{c.RoleId}|{c.ClaimType}|{c.ClaimValue}").Order());
    }

    [SqlServerFact]
    public async Task The_role_table_has_no_legacy_default_or_deleted_flags()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        var columns = await db.Database.SqlQuery<string>(
            $"SELECT name AS Value FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AspNetRoles')").ToListAsync();

        Assert.DoesNotContain("IsDefault", columns);
        Assert.DoesNotContain("IsDeleted", columns);
        Assert.Contains("IsSystem", columns);
    }
}

/// <summary>The in-memory test host seeds the same fixed roles at startup (so role-based assertions are meaningful).</summary>
public class RoleSeederTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    [Fact]
    public async Task Startup_seeds_exactly_the_fixed_roles()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        Assert.Equal(PermissionCatalog.Roles.Select(r => r.Name).Order(), (await roles.Roles.Select(r => r.Name!).ToListAsync()).Order());
        Assert.Null(await roles.FindByNameAsync("Admin"));
        Assert.Null(await roles.FindByNameAsync("User"));
    }
}
