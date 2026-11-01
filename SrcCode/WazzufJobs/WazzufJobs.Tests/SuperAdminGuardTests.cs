using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// C0-5 generalized (Phase 02, S3): the platform is never left without an active Super Admin. Runs on a throwaway
/// local SQL Server database so the role lock and concurrent requests behave as in production.
/// </summary>
public class SuperAdminGuardTests(SqlServerC0Factory factory) : IClassFixture<SqlServerC0Factory>
{
    private const string Password = "Guard#Passw0rd!S3";

    [SqlServerFact]
    public async Task The_only_active_super_admin_cannot_be_disabled_by_another_account()
    {
        await ResetSuperAdminsAsync();
        var only = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var operatorClient = Client(TestPersonas.SuperAdmin.NewUser());   // a token alone does not count as a Super Admin

        var response = await operatorClient.PutAsync($"/api/users/{only.Id}/toggle-status", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("User.LastAdministrator", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, await ActiveSuperAdminsAsync());
    }

    [SqlServerFact]
    public async Task Disabled_and_incompatible_holders_do_not_count_as_super_admins()
    {
        await ResetSuperAdminsAsync();
        var active = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var disabled = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin, disabled: true);
        var candidateHolder = await CreateAsync(AccountType.Candidate, PlatformRoles.SuperAdmin);   // grants nothing at sign-in
        var demoHolder = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin, isDemo: true);
        Assert.Equal(1, await ActiveSuperAdminsAsync());

        var client = Client(active);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/users/{active.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/users/{active.Id}",
            new { firstName = active.FirstName, lastName = active.LastName, email = active.Email, roles = Array.Empty<string>() })).StatusCode);

        // disabling the non-effective holders is not guarded (they are not active Super Admins)
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/users/{candidateHolder.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/users/{demoHolder.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(1, await ActiveSuperAdminsAsync());
        _ = disabled;
    }

    [SqlServerFact]
    public async Task With_two_active_super_admins_one_can_be_disabled_and_enabled_again()
    {
        await ResetSuperAdminsAsync();
        var keeper = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var other = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
        var client = Client(keeper);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/users/{other.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(1, await ActiveSuperAdminsAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/users/{keeper.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/users/{other.Id}/toggle-status", null)).StatusCode);
        Assert.Equal(2, await ActiveSuperAdminsAsync());
    }

    [SqlServerFact]
    public async Task Two_super_admins_disabling_each_other_concurrently_leave_exactly_one_active()
    {
        for (var round = 0; round < 8; round++)
        {
            await ResetSuperAdminsAsync();
            var a = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
            var b = await CreateAsync(AccountType.Staff, PlatformRoles.SuperAdmin);
            var clientA = Client(a);
            var clientB = Client(b);

            using var start = new SemaphoreSlim(0, 2);
            async Task<HttpResponseMessage> Disable(HttpClient client, AppUser target)
            {
                await start.WaitAsync();
                return await client.PutAsync($"/api/users/{target.Id}/toggle-status", null);
            }
            var both = Task.WhenAll(Disable(clientA, b), Disable(clientB, a));
            start.Release(2);
            var responses = await both;

            var statuses = responses.Select(r => r.StatusCode).Order().ToList();
            Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.BadRequest], statuses);
            Assert.Equal(1, await ActiveSuperAdminsAsync());
        }
    }

    private HttpClient Client(AppUser user) => factory.CreateClient().AsAdmin(factory.Services, user);

    private async Task<AppUser> CreateAsync(AccountType accountType, string role, bool disabled = false, bool isDemo = false)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"guard-{Guid.NewGuid():N}@example.test";
        var user = new AppUser
        {
            UserName = email, Email = email, FirstName = "Guard", LastName = "Test", EmailConfirmed = true,
            AccountType = accountType, IsDemo = isDemo, IsDisabled = disabled
        };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    private async Task<int> ActiveSuperAdminsAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .CountActiveUsersInRoleAsync(PermissionCatalog.SuperAdmin, CancellationToken.None);
    }

    private async Task ResetSuperAdminsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var existing in await users.GetUsersInRoleAsync(PlatformRoles.SuperAdmin))
            await users.RemoveFromRoleAsync(existing, PlatformRoles.SuperAdmin);
    }
}
