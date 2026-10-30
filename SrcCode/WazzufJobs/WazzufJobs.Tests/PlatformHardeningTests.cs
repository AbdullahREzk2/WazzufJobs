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
    public async Task No_administrator_is_ever_created_automatically()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        Assert.Null(await users.FindByEmailAsync("admin@wazzuf.com"));
        // the former bootstrap settings are configured in this host and must be ignored
        Assert.Null(await users.FindByEmailAsync(C0WebApplicationFactory.IgnoredBootstrapEmail));
        Assert.Empty(await users.GetUsersInRoleAsync(AppRoles.Admin.Name));
    }

    [Fact]
    public async Task Without_an_administrator_admin_endpoints_fail_closed()
    {
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().GetUsersInRoleAsync(AppRoles.Admin.Name));

        var s = await TestData.SeedAsync(factory.Services);
        var candidate = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);
        var anonymous = factory.CreateClient();

        foreach (var url in new[] { "/api/users", $"/api/users/{s.CandidateB.Id}", "/api/roles", $"/api/applications/job/{s.Job.Id}" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await candidate.GetAsync(url)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.PostAsJsonAsync("/api/users",
            new { firstName = "x", lastName = "y", email = "self-made-admin@example.test", password = "Self#Made1!", roles = new[] { "Admin" } })).StatusCode);
    }

    [Fact]
    public async Task Self_registration_never_grants_administrator_rights()
    {
        var email = $"register-{Guid.NewGuid():N}@example.test";
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email, password = "Register#Passw0rd1", firstName = "New", lastName = "User" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(await users.IsInRoleAsync(user!, AppRoles.Admin.Name));
    }

    [Fact]
    public void Shipped_assemblies_contain_no_default_administrator_credentials()
    {
        var assemblies = new[] { typeof(WazzufJobs.API.Program), typeof(WazzufJobs.BLL.Abstractions.Result), typeof(AppUser) }
            .Select(t => t.Assembly.Location);

        foreach (var path in assemblies)
        {
            var bytes = File.ReadAllBytes(path);
            foreach (var secret in new[] { "Admin@123456", "admin@wazzuf.com" })
            {
                Assert.False(Contains(bytes, System.Text.Encoding.Unicode.GetBytes(secret)), $"{Path.GetFileName(path)} contains '{secret}' (UTF-16)");
                Assert.False(Contains(bytes, System.Text.Encoding.UTF8.GetBytes(secret)), $"{Path.GetFileName(path)} contains '{secret}' (UTF-8)");
            }
        }

        static bool Contains(byte[] haystack, byte[] needle) =>
            haystack.AsSpan().IndexOf(needle) >= 0;
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
