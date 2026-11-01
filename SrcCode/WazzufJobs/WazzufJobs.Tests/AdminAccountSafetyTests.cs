using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Owner action "change the admin email" must keep the Super Admin role and a working login; the platform must never
/// be left without an active Super Admin (C0-5, generalized in Phase 02 S3). Runs on a throwaway local SQL Server database (relational behaviour).
/// </summary>
public class AdminAccountSafetyTests(SqlServerC0Factory factory) : IClassFixture<SqlServerC0Factory>
{
    private const string Password = "Admin#Passw0rd!C0";

    [SqlServerFact]
    public async Task Admin_can_change_own_email_and_keeps_role_and_login()
    {
        var admin = await CreateAdminAsync();
        var newEmail = $"owner-{Guid.NewGuid():N}@example.test";
        var client = factory.CreateClient().AsAdmin(factory.Services, admin);

        // the same payload the admin "Edit user" form sends (current roles pre-selected)
        var update = await client.PutAsJsonAsync($"/api/users/{admin.Id}",
            new { firstName = admin.FirstName, lastName = admin.LastName, email = newEmail, roles = new[] { PlatformRoles.SuperAdmin } });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var reloaded = await users.FindByIdAsync(admin.Id);
            Assert.Equal(newEmail, reloaded!.Email);
            Assert.Equal(newEmail, reloaded.UserName);
            Assert.True(reloaded.EmailConfirmed);
            Assert.True(await users.IsInRoleAsync(reloaded, PlatformRoles.SuperAdmin));
        }

        var anonymous = factory.CreateClient();
        var newLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = newEmail, password = Password });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        // the fresh token still carries admin permissions
        var token = (await newLogin.Content.ReadFromJsonAsync<LoginResult>())!.Token;
        var adminCall = factory.CreateClient();
        adminCall.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await adminCall.GetAsync("/api/users")).StatusCode);

        var oldLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = admin.Email, password = Password });
        Assert.NotEqual(HttpStatusCode.OK, oldLogin.StatusCode);
    }

    [SqlServerFact]
    public async Task The_last_administrator_cannot_lose_the_admin_role_or_be_disabled()
    {
        await RemoveAllOtherAdminsAsync();
        var admin = await CreateAdminAsync();
        var client = factory.CreateClient().AsAdmin(factory.Services, admin);

        var demote = await client.PutAsJsonAsync($"/api/users/{admin.Id}",
            new { firstName = admin.FirstName, lastName = admin.LastName, email = admin.Email, roles = Array.Empty<string>() });
        var disable = await client.PutAsync($"/api/users/{admin.Id}/toggle-status", null);

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, disable.StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var reloaded = await users.FindByIdAsync(admin.Id);
        Assert.True(await users.IsInRoleAsync(reloaded!, PlatformRoles.SuperAdmin));
        Assert.False(reloaded!.IsDisabled);
    }

    // Phase 02 (S3, D-STAFF-ROLES): this C0 scenario (an administrator removing another administrator's role through
    // the Users API) is now refused: staff roles are never granted or revoked there. The role is kept.
    [SqlServerFact]
    public async Task Staff_roles_cannot_be_revoked_through_the_users_api_even_when_another_super_admin_exists()
    {
        var keeper = await CreateAdminAsync();
        var other = await CreateAdminAsync();
        var client = factory.CreateClient().AsAdmin(factory.Services, keeper);

        var response = await client.PutAsJsonAsync($"/api/users/{other.Id}",
            new { firstName = other.FirstName, lastName = other.LastName, email = other.Email, roles = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Users.StaffRolesManagedSeparately", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Equal([PlatformRoles.SuperAdmin], await users.GetRolesAsync((await users.FindByIdAsync(other.Id))!));
        Assert.True(await users.IsInRoleAsync((await users.FindByIdAsync(keeper.Id))!, PlatformRoles.SuperAdmin));
    }

    // the C0 "apply only differences" behaviour, on the role the Users API can still change (an empty list is valid)
    [SqlServerFact]
    public async Task Role_changes_apply_only_differences()
    {
        var admin = await CreateAdminAsync();
        var client = factory.CreateClient().AsAdmin(factory.Services, admin);
        AppUser candidate;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            candidate = TestData.NewUser("candidate");
            Assert.True((await users.CreateAsync(candidate, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(candidate, PlatformRoles.Candidate)).Succeeded);
        }

        async Task<IList<string>> RolesAfter(string[] roles)
        {
            var response = await client.PutAsJsonAsync($"/api/users/{candidate.Id}",
                new { firstName = candidate.FirstName, lastName = candidate.LastName, email = candidate.Email, roles });
            Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            return await users.GetRolesAsync((await users.FindByIdAsync(candidate.Id))!);
        }

        Assert.Equal([PlatformRoles.Candidate], await RolesAfter([PlatformRoles.Candidate]));
        Assert.Empty(await RolesAfter([]));
        Assert.Equal([PlatformRoles.Candidate], await RolesAfter([PlatformRoles.Candidate]));
    }

    private async Task<AppUser> CreateAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        var admin = new AppUser { UserName = email, Email = email, FirstName = "Site", LastName = "Admin", EmailConfirmed = true, AccountType = AccountType.Staff };
        Assert.True((await users.CreateAsync(admin, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, PlatformRoles.SuperAdmin)).Succeeded);
        return admin;
    }

    private async Task RemoveAllOtherAdminsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var existing in await users.GetUsersInRoleAsync(PlatformRoles.SuperAdmin))
            await users.RemoveFromRoleAsync(existing, PlatformRoles.SuperAdmin);
    }

    private record LoginResult(string Token);
}
