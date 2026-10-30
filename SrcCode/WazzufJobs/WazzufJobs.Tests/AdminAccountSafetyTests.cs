using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Owner action "change the admin email" must keep the Admin role and a working login; the platform must never
/// be left without an administrator. Runs on a throwaway local SQL Server database (relational behaviour).
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
            new { firstName = admin.FirstName, lastName = admin.LastName, email = newEmail, roles = new[] { AppRoles.Admin.Name } });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var reloaded = await users.FindByIdAsync(admin.Id);
            Assert.Equal(newEmail, reloaded!.Email);
            Assert.Equal(newEmail, reloaded.UserName);
            Assert.True(reloaded.EmailConfirmed);
            Assert.True(await users.IsInRoleAsync(reloaded, AppRoles.Admin.Name));
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
        Assert.True(await users.IsInRoleAsync(reloaded!, AppRoles.Admin.Name));
        Assert.False(reloaded!.IsDisabled);
    }

    // note: the default "User" role is rejected by the existing role validation (pre-existing behaviour),
    // so the admin-only demotion scenario is exercised with an empty role list
    [SqlServerFact]
    public async Task Role_changes_apply_only_differences_when_another_admin_exists()
    {
        var keeper = await CreateAdminAsync();
        var other = await CreateAdminAsync();
        var client = factory.CreateClient().AsAdmin(factory.Services, keeper);

        var response = await client.PutAsJsonAsync($"/api/users/{other.Id}",
            new { firstName = other.FirstName, lastName = other.LastName, email = other.Email, roles = Array.Empty<string>() });

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = await users.GetRolesAsync((await users.FindByIdAsync(other.Id))!);
        Assert.Empty(roles);
        Assert.True(await users.IsInRoleAsync((await users.FindByIdAsync(keeper.Id))!, AppRoles.Admin.Name));
    }

    private async Task<AppUser> CreateAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        var admin = new AppUser { UserName = email, Email = email, FirstName = "Site", LastName = "Admin", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, AppRoles.Admin.Name)).Succeeded);
        return admin;
    }

    private async Task RemoveAllOtherAdminsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var existing in await users.GetUsersInRoleAsync(AppRoles.Admin.Name))
            await users.RemoveFromRoleAsync(existing, AppRoles.Admin.Name);
    }

    private record LoginResult(string Token);
}
