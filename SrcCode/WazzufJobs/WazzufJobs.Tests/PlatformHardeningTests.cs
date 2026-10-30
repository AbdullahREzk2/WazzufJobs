using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>S2, S5, S13 and session hardening.</summary>
public class PlatformHardeningTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    [Theory]
    [InlineData("/hangfire")]
    [InlineData("/hangfire?u=admin&p=2003")]
    [InlineData("/hangfire/jobs/enqueued")]
    public async Task Hangfire_dashboard_is_not_reachable(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/swagger/index.html")]
    [InlineData("/index.html")]
    public async Task Swagger_is_not_exposed_in_production(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous_and_reveals_no_details()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task No_administrator_is_seeded_from_hardcoded_credentials()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        Assert.Null(await users.FindByEmailAsync("admin@wazzuf.com"));
    }

    [Fact]
    public async Task Admin_seeder_requires_configuration_and_never_modifies_an_existing_account()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"bootstrap-{Guid.NewGuid():N}@example.test";

        Assert.Equal(AdminSeedOutcome.SkippedNotConfigured, await AdminSeeder.SeedAsync(users, null, null));
        Assert.Equal(AdminSeedOutcome.SkippedNotConfigured, await AdminSeeder.SeedAsync(users, email, ""));

        Assert.Equal(AdminSeedOutcome.Created, await AdminSeeder.SeedAsync(users, email, "Bootstrap#Passw0rd!"));
        var admin = await users.FindByEmailAsync(email);
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin!, AppRoles.Admin.Name));

        // a second start with different configured credentials must not reset the password
        Assert.Equal(AdminSeedOutcome.SkippedAlreadyExists, await AdminSeeder.SeedAsync(users, email, "Another#Passw0rd!"));
        Assert.True(await users.CheckPasswordAsync((await users.FindByEmailAsync(email))!, "Bootstrap#Passw0rd!"));
    }

    [Fact]
    public async Task Changing_the_password_revokes_all_existing_refresh_tokens()
    {
        AppUser user;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            user = TestData.NewUser("rotating-admin");
            Assert.True((await users.CreateAsync(user, "Original#Passw0rd!")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            db.RefreshTokens.AddRange(
                new RefreshToken { UserId = user.Id, Token = "t1", ExpiresOn = DateTime.UtcNow.AddDays(10) },
                new RefreshToken { UserId = user.Id, Token = "t2", ExpiresOn = DateTime.UtcNow.AddDays(10) });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient().AsAdmin(factory.Services, user);
        var response = await client.PutAsJsonAsync("/api/account/change-password",
            new { currentPassword = "Original#Passw0rd!", newPassword = "Rotated#Passw0rd!2" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var tokens = await verify.ServiceProvider.GetRequiredService<ApplicationDBContext>()
            .RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedOn));
    }
}

public class AuthRateLimitTests(StrictRateLimitFactory factory) : IClassFixture<StrictRateLimitFactory>
{
    [Fact]
    public async Task Login_attempts_beyond_the_limit_are_rejected_with_429()
    {
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < StrictRateLimitFactory.Limit + 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "nobody@example.test", password = "Wrong#Passw0rd" });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(StrictRateLimitFactory.Limit), s => Assert.NotEqual(HttpStatusCode.TooManyRequests, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses.Last());
    }

    [Fact]
    public async Task Rate_limit_probe_with_unknown_emails_never_touches_a_real_account_lockout()
    {
        // the production probe uses random non-existent emails: the login handler rejects them before
        // any password check, so no account's failed-attempt counter or lockout can change
        string adminId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var admin = TestData.NewUser("probe-admin");
            Assert.True((await users.CreateAsync(admin, "Probe#Passw0rd!1")).Succeeded);
            adminId = admin.Id;
        }

        var client = factory.CreateClient();
        for (var i = 0; i < StrictRateLimitFactory.Limit + 3; i++)
            await client.PostAsJsonAsync("/api/auth/login",
                new { email = $"ratelimit-probe-{Guid.NewGuid():N}@example.invalid", password = "Wrong#Passw0rd1" });

        using var verify = factory.Services.CreateScope();
        var reloaded = await verify.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(adminId);
        Assert.Equal(0, reloaded!.AccessFailedCount);
        Assert.Null(reloaded.LockoutEnd);
    }
}
