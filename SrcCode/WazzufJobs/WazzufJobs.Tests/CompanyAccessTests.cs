using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Features.Companies.Commands.ChangeMemberRole;
using WazzufJobs.BLL.Features.Companies.Commands.RemoveMember;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Shared;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Company membership and tenant isolation (Phase 02, S4; invariant P02-2). Company A vs company B, removed members,
/// suspended companies, staff and candidates, every role × permission, the last-owner guard and concurrent membership
/// changes. Runs on a throwaway local SQL Server database; every test builds its own companies.
/// </summary>
public class CompanyAccessTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    // ---------------------------------------------------------------- scenario

    private sealed record Tenant(Company Company, AppUser Owner, AppUser Admin, AppUser Recruiter, AppUser Removed);

    private async Task<Tenant> TenantAsync(CompanyStatus status = CompanyStatus.Active, bool isDemo = false)
    {
        var company = new Company { Id = Guid.NewGuid(), Name = "Tenant", Slug = $"tenant-{Guid.NewGuid():N}", Status = status, IsDemo = isDemo, LogoKey = "logos/secret-key" };
        var owner = CompanyAccount("owner", isDemo);
        var admin = CompanyAccount("admin", isDemo);
        var recruiter = CompanyAccount("recruiter", isDemo);
        var removed = CompanyAccount("removed", isDemo);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        db.AddRange(company, owner, admin, recruiter, removed,
            Member(company, owner, CompanyMemberRole.Owner),
            Member(company, admin, CompanyMemberRole.Admin),
            Member(company, recruiter, CompanyMemberRole.Recruiter),
            Member(company, removed, CompanyMemberRole.Admin, CompanyMemberStatus.Removed));
        await db.SaveChangesAsync();
        return new Tenant(company, owner, admin, recruiter, removed);
    }

    private static AppUser CompanyAccount(string label, bool isDemo = false)
    {
        var user = TestPersonas.CompanyUser.NewUser();
        user.FirstName = label;
        user.IsDemo = isDemo;
        return user;
    }

    private static CompanyMember Member(Company company, AppUser user, CompanyMemberRole role, CompanyMemberStatus status = CompanyMemberStatus.Active) =>
        new() { CompanyId = company.Id, UserId = user.Id, Role = role, Status = status };

    private async Task<AppUser> AddAsync(AppUser user, params CompanyMember[] memberships)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        db.Add(user);
        db.AddRange(memberships);
        await db.SaveChangesAsync();
        return user;
    }

    private HttpClient Client(Persona persona, AppUser user) => factory.CreateClient().As(factory.Services, persona, user);
    private HttpClient Member(AppUser user) => Client(TestPersonas.CompanyUser, user);

    // ---------------------------------------------------------------- HTTP: members and outsiders

    [SqlServerFact]
    public async Task Every_active_member_sees_its_company_and_active_team_with_its_own_role()
    {
        var a = await TenantAsync();

        foreach (var (user, role) in new[] { (a.Owner, "Owner"), (a.Admin, "Admin"), (a.Recruiter, "Recruiter") })
        {
            var client = Member(user);
            var company = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/companies/{a.Company.Id}")).RootElement;
            Assert.Equal(a.Company.Id, company.GetProperty("id").GetGuid());
            Assert.Equal(role, company.GetProperty("myRole").GetString());
            Assert.Equal("Active", company.GetProperty("status").GetString());

            var members = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/companies/{a.Company.Id}/members")).RootElement;
            Assert.Equal(
                new[] { a.Owner.Id, a.Admin.Id, a.Recruiter.Id }.Order(),
                members.EnumerateArray().Select(m => m.GetProperty("userId").GetString()!).Order());
        }
    }

    [SqlServerFact]
    public async Task Responses_contain_only_the_documented_fields()
    {
        var a = await TenantAsync();
        var client = Member(a.Owner);

        var company = await client.GetStringAsync($"/api/v1/companies/{a.Company.Id}");
        var members = await client.GetStringAsync($"/api/v1/companies/{a.Company.Id}/members");

        Assert.Equal(
            new[] { "id", "name", "slug", "website", "industry", "size", "location", "description", "verificationStatus", "status", "createdAt", "myRole" }.Order(),
            TestData.PropertyNames(company).Order());
        Assert.Equal(new[] { "userId", "firstName", "lastName", "email", "role", "joinedAt" }.Order(), TestData.PropertyNames(members).Order());
        foreach (var marker in new[] { "logos/secret-key", "rowVersion", "isDemo", "isPlatform", "passwordHash", "securityStamp", a.Removed.Email! })
            Assert.DoesNotContain(marker, company + members, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task Another_tenant_a_removed_member_staff_and_candidates_get_the_same_404_as_an_unknown_company()
    {
        var a = await TenantAsync();
        var b = await TenantAsync();
        var outsiders = new List<(string Who, HttpClient Client)>
        {
            ("company B owner", Member(b.Owner)),
            ("company B admin", Member(b.Admin)),
            ("removed member of A", Member(a.Removed)),
            ("super admin", Client(TestPersonas.SuperAdmin, await AddAsync(TestPersonas.SuperAdmin.NewUser()))),
            ("moderator", Client(TestPersonas.Moderator, await AddAsync(TestPersonas.Moderator.NewUser()))),
            ("demo admin", Client(TestPersonas.DemoAdmin, await AddAsync(TestPersonas.DemoAdmin.NewUser()))),
            ("candidate", Client(TestPersonas.Candidate, await AddAsync(TestPersonas.Candidate.NewUser()))),
            ("company account without membership", Member(await AddAsync(CompanyAccount("loner"))))
        };

        var unknown = await Member(a.Owner).GetAsync($"/api/v1/companies/{Guid.NewGuid()}");
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("Company.NotFound", unknownBody);

        foreach (var (who, client) in outsiders)
            foreach (var url in new[] { $"/api/v1/companies/{a.Company.Id}", $"/api/v1/companies/{a.Company.Id}/members" })
            {
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{who} GET {url} -> {(int)response.StatusCode}");
                Assert.Equal(unknownBody, await response.Content.ReadAsStringAsync());
            }

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/companies/{a.Company.Id}")).StatusCode);
    }

    [SqlServerFact]
    public async Task Members_of_a_suspended_company_see_only_its_profile()
    {
        var s = await TenantAsync(CompanyStatus.Suspended);

        foreach (var user in new[] { s.Owner, s.Admin, s.Recruiter })
        {
            var client = Member(user);
            var company = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/companies/{s.Company.Id}")).RootElement;
            Assert.Equal("Suspended", company.GetProperty("status").GetString());

            var members = await client.GetAsync($"/api/v1/companies/{s.Company.Id}/members");
            Assert.Equal(HttpStatusCode.Forbidden, members.StatusCode);
            Assert.Contains("Company.Suspended", await members.Content.ReadAsStringAsync());
        }
    }

    [SqlServerFact]
    public async Task Memberships_that_must_not_grant_access_are_ignored()
    {
        var a = await TenantAsync();
        var platform = PlatformCompany.Id;
        // a candidate account, a disabled company account, a demo account in a real company, a platform-company member
        var candidate = TestPersonas.Candidate.NewUser();
        var disabled = CompanyAccount("disabled");
        disabled.IsDisabled = true;
        var demo = CompanyAccount("demo", isDemo: true);
        var platformMember = CompanyAccount("platform");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            db.AddRange(candidate, disabled, demo, platformMember,
                new CompanyMember { CompanyId = a.Company.Id, UserId = candidate.Id, Role = CompanyMemberRole.Owner },
                new CompanyMember { CompanyId = a.Company.Id, UserId = disabled.Id, Role = CompanyMemberRole.Owner },
                new CompanyMember { CompanyId = a.Company.Id, UserId = demo.Id, Role = CompanyMemberRole.Owner },
                new CompanyMember { CompanyId = platform, UserId = platformMember.Id, Role = CompanyMemberRole.Owner });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await Client(TestPersonas.Candidate, candidate).GetAsync($"/api/v1/companies/{a.Company.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Member(disabled).GetAsync($"/api/v1/companies/{a.Company.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Member(demo).GetAsync($"/api/v1/companies/{a.Company.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Member(platformMember).GetAsync($"/api/v1/companies/{platform}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Member(platformMember).GetAsync($"/api/v1/companies/{platform}/members")).StatusCode);
    }

    [SqlServerFact]
    public async Task A_demo_company_is_invisible_in_the_real_data_scope_even_to_its_demo_members()
    {
        var d = await TenantAsync(isDemo: true);

        Assert.Equal(HttpStatusCode.NotFound, (await Member(d.Owner).GetAsync($"/api/v1/companies/{d.Company.Id}")).StatusCode);
    }

    // ---------------------------------------------------------------- ICompanyAccessService, exhaustively

    // hand-written specification (architecture §3), independent of CompanyPermissionCatalog
    private static readonly Dictionary<CompanyMemberRole, CompanyPermission[]> Spec = new()
    {
        [CompanyMemberRole.Owner] = Enum.GetValues<CompanyPermission>(),
        [CompanyMemberRole.Admin] =
        [
            CompanyPermission.ViewCompany, CompanyPermission.ViewMembers, CompanyPermission.EditProfile, CompanyPermission.ManageTeam,
            CompanyPermission.ManageJobs, CompanyPermission.ManageApplicants
        ],
        [CompanyMemberRole.Recruiter] =
            [CompanyPermission.ViewCompany, CompanyPermission.ViewMembers, CompanyPermission.ManageJobs, CompanyPermission.ManageApplicants]
    };

    [SqlServerFact]
    public async Task Access_service_grants_exactly_the_specified_permissions_per_role_and_company_status()
    {
        var active = await TenantAsync();
        var suspended = await TenantAsync(CompanyStatus.Suspended);
        var other = await TenantAsync();

        foreach (var tenant in new[] { active, suspended })
            foreach (var (user, role) in new[] { (tenant.Owner, CompanyMemberRole.Owner), (tenant.Admin, CompanyMemberRole.Admin), (tenant.Recruiter, CompanyMemberRole.Recruiter) })
                foreach (var permission in Enum.GetValues<CompanyPermission>())
                {
                    var result = await AccessAsync(user, tenant.Company.Id, permission);
                    var label = $"{role} {permission} ({tenant.Company.Status})";
                    if (tenant.Company.Status == CompanyStatus.Suspended && permission != CompanyPermission.ViewCompany)
                        Assert.True(result.Error.Code == "Company.Suspended", label);
                    else if (!Spec[role].Contains(permission))
                        Assert.True(result.Error.Code == "Company.PermissionDenied", label);
                    else
                    {
                        Assert.True(result.IsSuccess, label);
                        Assert.Equal(role, result.Value.Role);
                        Assert.Equal(tenant.Company.Id, result.Value.CompanyId);
                    }
                }

        foreach (var permission in Enum.GetValues<CompanyPermission>())
        {
            Assert.Equal("Company.NotFound", (await AccessAsync(other.Owner, active.Company.Id, permission)).Error.Code);
            Assert.Equal("Company.NotFound", (await AccessAsync(active.Removed, active.Company.Id, permission)).Error.Code);
            Assert.Equal("Company.NotFound", (await AccessAsync(active.Owner, Guid.NewGuid(), permission)).Error.Code);
        }
        Assert.Equal("Company.NotFound", (await AccessAsync(null, active.Company.Id, CompanyPermission.ViewCompany)).Error.Code);
    }

    [Fact]
    public void Company_permission_catalog_matches_the_specification()
    {
        foreach (var (role, permissions) in Spec)
            Assert.Equal(permissions.Order(), CompanyPermissionCatalog.PermissionsOf(role).Order());
        Assert.Equal([CompanyPermission.ViewCompany], CompanyPermissionCatalog.AllowedWhileSuspended);
        Assert.Empty(CompanyPermissionCatalog.PermissionsOf((CompanyMemberRole)99));
    }

    // ---------------------------------------------------------------- team management and the last-owner guard

    [SqlServerFact]
    public async Task Owners_manage_everyone_admins_manage_non_owners_recruiters_manage_nobody()
    {
        var a = await TenantAsync();
        var extra = await AddMemberAsync(a.Company, CompanyMemberRole.Recruiter);

        // admin: non-owners only
        Assert.True((await ChangeRoleAsync(a.Admin, a.Company.Id, extra.Id, CompanyMemberRole.Admin)).IsSuccess);
        Assert.Equal("Company.OwnersManagedByOwners", (await ChangeRoleAsync(a.Admin, a.Company.Id, extra.Id, CompanyMemberRole.Owner)).Error.Code);
        Assert.Equal("Company.OwnersManagedByOwners", (await ChangeRoleAsync(a.Admin, a.Company.Id, a.Owner.Id, CompanyMemberRole.Recruiter)).Error.Code);
        Assert.Equal("Company.OwnersManagedByOwners", (await RemoveAsync(a.Admin, a.Company.Id, a.Owner.Id)).Error.Code);

        // recruiter: nothing
        Assert.Equal("Company.PermissionDenied", (await ChangeRoleAsync(a.Recruiter, a.Company.Id, extra.Id, CompanyMemberRole.Recruiter)).Error.Code);
        Assert.Equal("Company.PermissionDenied", (await ChangeRoleAsync(a.Recruiter, a.Company.Id, a.Recruiter.Id, CompanyMemberRole.Owner)).Error.Code);
        Assert.Equal("Company.PermissionDenied", (await RemoveAsync(a.Recruiter, a.Company.Id, extra.Id)).Error.Code);

        // owner: anyone, including making a second owner
        Assert.True((await ChangeRoleAsync(a.Owner, a.Company.Id, extra.Id, CompanyMemberRole.Owner)).IsSuccess);
        Assert.True((await ChangeRoleAsync(a.Owner, a.Company.Id, extra.Id, CompanyMemberRole.Recruiter)).IsSuccess);
        Assert.True((await RemoveAsync(a.Admin, a.Company.Id, extra.Id)).IsSuccess);

        Assert.Equal(CompanyMemberStatus.Removed, (await MembershipAsync(a.Company.Id, extra.Id))!.Status);
        Assert.Equal(CompanyMemberRole.Owner, (await MembershipAsync(a.Company.Id, a.Owner.Id))!.Role);
        Assert.Equal(HttpStatusCode.NotFound, (await Member(extra).GetAsync($"/api/v1/companies/{a.Company.Id}")).StatusCode);
    }

    [SqlServerFact]
    public async Task Team_changes_never_cross_tenants_or_touch_removed_members()
    {
        var a = await TenantAsync();
        var b = await TenantAsync();

        Assert.Equal("Company.NotFound", (await RemoveAsync(b.Owner, a.Company.Id, a.Recruiter.Id)).Error.Code);
        Assert.Equal("Company.NotFound", (await ChangeRoleAsync(b.Owner, a.Company.Id, a.Recruiter.Id, CompanyMemberRole.Owner)).Error.Code);
        Assert.Equal("Company.MemberNotFound", (await RemoveAsync(a.Owner, a.Company.Id, b.Recruiter.Id)).Error.Code);
        Assert.Equal("Company.MemberNotFound", (await ChangeRoleAsync(a.Owner, a.Company.Id, b.Owner.Id, CompanyMemberRole.Recruiter)).Error.Code);
        Assert.Equal("Company.MemberNotFound", (await ChangeRoleAsync(a.Owner, a.Company.Id, a.Removed.Id, CompanyMemberRole.Owner)).Error.Code);
        Assert.Equal("Company.NotFound", (await ChangeRoleAsync(a.Removed, a.Company.Id, a.Removed.Id, CompanyMemberRole.Owner)).Error.Code);

        Assert.Equal(CompanyMemberRole.Recruiter, (await MembershipAsync(a.Company.Id, a.Recruiter.Id))!.Role);
        Assert.Equal(CompanyMemberStatus.Active, (await MembershipAsync(b.Company.Id, b.Recruiter.Id))!.Status);
        Assert.Equal(CompanyMemberStatus.Removed, (await MembershipAsync(a.Company.Id, a.Removed.Id))!.Status);
    }

    [SqlServerFact]
    public async Task The_last_active_owner_cannot_be_demoted_or_removed()
    {
        var a = await TenantAsync();

        Assert.Equal("Company.LastOwner", (await ChangeRoleAsync(a.Owner, a.Company.Id, a.Owner.Id, CompanyMemberRole.Admin)).Error.Code);
        Assert.Equal("Company.LastOwner", (await RemoveAsync(a.Owner, a.Company.Id, a.Owner.Id)).Error.Code);

        // a disabled second owner does not count, but may itself be demoted
        var disabledOwner = CompanyAccount("disabled-owner");
        disabledOwner.IsDisabled = true;
        await AddAsync(disabledOwner, new CompanyMember { CompanyId = a.Company.Id, UserId = disabledOwner.Id, Role = CompanyMemberRole.Owner });
        Assert.Equal("Company.LastOwner", (await RemoveAsync(a.Owner, a.Company.Id, a.Owner.Id)).Error.Code);
        Assert.True((await ChangeRoleAsync(a.Owner, a.Company.Id, disabledOwner.Id, CompanyMemberRole.Recruiter)).IsSuccess);

        // with a second active owner, either may step down
        var second = await AddMemberAsync(a.Company, CompanyMemberRole.Owner);
        Assert.True((await ChangeRoleAsync(a.Owner, a.Company.Id, a.Owner.Id, CompanyMemberRole.Admin)).IsSuccess);
        Assert.Equal("Company.LastOwner", (await RemoveAsync(second, a.Company.Id, second.Id)).Error.Code);
        Assert.Equal(CompanyMemberRole.Owner, (await MembershipAsync(a.Company.Id, second.Id))!.Role);
    }

    [SqlServerFact]
    public async Task Suspended_companies_cannot_change_their_team()
    {
        var s = await TenantAsync(CompanyStatus.Suspended);

        Assert.Equal("Company.Suspended", (await RemoveAsync(s.Owner, s.Company.Id, s.Recruiter.Id)).Error.Code);
        Assert.Equal("Company.Suspended", (await ChangeRoleAsync(s.Owner, s.Company.Id, s.Recruiter.Id, CompanyMemberRole.Admin)).Error.Code);
        Assert.Equal(CompanyMemberStatus.Active, (await MembershipAsync(s.Company.Id, s.Recruiter.Id))!.Status);
    }

    // ---------------------------------------------------------------- concurrency

    [SqlServerFact]
    public async Task Two_owners_stepping_down_at_the_same_time_leave_exactly_one_owner()
    {
        for (var round = 0; round < 8; round++)
        {
            var a = await TenantAsync();
            var second = await AddMemberAsync(a.Company, CompanyMemberRole.Owner);

            var results = await Concurrently(
                () => ChangeRoleAsync(a.Owner, a.Company.Id, a.Owner.Id, CompanyMemberRole.Admin),
                () => ChangeRoleAsync(second, a.Company.Id, second.Id, CompanyMemberRole.Admin));

            Assert.Equal(1, results.Count(r => r.IsSuccess));
            Assert.Equal("Company.LastOwner", results.Single(r => !r.IsSuccess).Error.Code);
            Assert.Equal(1, await OwnersAsync(a.Company.Id));
        }
    }

    [SqlServerFact]
    public async Task Two_owners_removing_each_other_at_the_same_time_leave_exactly_one_owner()
    {
        for (var round = 0; round < 8; round++)
        {
            var a = await TenantAsync();
            var second = await AddMemberAsync(a.Company, CompanyMemberRole.Owner);

            var results = await Concurrently(
                () => RemoveAsync(a.Owner, a.Company.Id, second.Id),
                () => RemoveAsync(second, a.Company.Id, a.Owner.Id));

            Assert.Equal(1, results.Count(r => r.IsSuccess));
            Assert.Equal(1, await OwnersAsync(a.Company.Id));
        }
    }

    [SqlServerFact]
    public async Task A_user_joining_two_companies_at_the_same_time_ends_up_in_exactly_one()
    {
        for (var round = 0; round < 8; round++)
        {
            var a = await TenantAsync();
            var b = await TenantAsync();
            var user = await AddAsync(CompanyAccount("joiner"));

            async Task<bool> Join(Guid companyId)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
                db.Add(new CompanyMember { CompanyId = companyId, UserId = user.Id, Role = CompanyMemberRole.Recruiter });
                try { await db.SaveChangesAsync(); return true; }
                catch (DbUpdateException) { return false; }
            }

            var joined = await Concurrently(() => Join(a.Company.Id), () => Join(b.Company.Id));

            Assert.Equal(1, joined.Count(j => j));
            using var verify = factory.Services.CreateScope();
            Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<ApplicationDBContext>()
                .CompanyMembers.CountAsync(m => m.UserId == user.Id && m.Status == CompanyMemberStatus.Active));
        }
    }

    // ---------------------------------------------------------------- helpers

    private async Task<AppUser> AddMemberAsync(Company company, CompanyMemberRole role)
    {
        var user = CompanyAccount(role.ToString().ToLowerInvariant());
        return await AddAsync(user, new CompanyMember { CompanyId = company.Id, UserId = user.Id, Role = role });
    }

    private sealed class FixedUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
    }

    private async Task<Result<CompanyAccess>> AccessAsync(AppUser? user, Guid companyId, CompanyPermission permission)
    {
        using var scope = factory.Services.CreateScope();
        var service = new CompanyAccessService(new FixedUser(user?.Id), scope.ServiceProvider.GetRequiredService<ICompanyRepository>());
        return await service.RequireAsync(companyId, permission, CancellationToken.None);
    }

    // the handlers exactly as P04's endpoints will run them, with the caller as the current user
    private async Task<Result> ChangeRoleAsync(AppUser actor, Guid companyId, string userId, CompanyMemberRole role)
    {
        using var scope = factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompanyRepository>();
        var handler = new ChangeCompanyMemberRoleCommandHandler(new CompanyAccessService(new FixedUser(actor.Id), companies), companies);
        return await handler.Handle(new ChangeCompanyMemberRoleCommand(companyId, userId, role), CancellationToken.None);
    }

    private async Task<Result> RemoveAsync(AppUser actor, Guid companyId, string userId)
    {
        using var scope = factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompanyRepository>();
        var handler = new RemoveCompanyMemberCommandHandler(new CompanyAccessService(new FixedUser(actor.Id), companies), companies);
        return await handler.Handle(new RemoveCompanyMemberCommand(companyId, userId), CancellationToken.None);
    }

    private async Task<CompanyMember?> MembershipAsync(Guid companyId, string userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().CompanyMembers.AsNoTracking()
            .SingleOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == userId);
    }

    private async Task<int> OwnersAsync(Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICompanyRepository>().CountActiveOwnersAsync(companyId, CancellationToken.None);
    }

    private static async Task<T[]> Concurrently<T>(params Func<Task<T>>[] actions)
    {
        using var start = new SemaphoreSlim(0, actions.Length);
        var tasks = actions.Select(async action => { await start.WaitAsync(); return await action(); }).ToArray();
        start.Release(actions.Length);
        return await Task.WhenAll(tasks);
    }
}
