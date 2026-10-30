using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WazzufJobs.DAL.Entities;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// C0 production state: Google:ClientId is not configured. Google sign-in must be refused without contacting
/// Google and without creating accounts, while e-mail/password authentication keeps working.
/// </summary>
public class GoogleLoginDisabledTests(GoogleNotConfiguredFactory factory) : IClassFixture<GoogleNotConfiguredFactory>
{
    [Fact]
    public async Task Google_sign_in_is_refused_with_503_and_contacts_nobody()
    {
        var email = $"google-off-{Guid.NewGuid():N}@example.test";
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/google",
            new { idToken = FakeGoogle.Token(email, "\"true\"") });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Google.Disabled", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, OutboundCallRecorder.Calls);
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task Email_and_password_sign_in_still_works()
    {
        var email = $"password-user-{Guid.NewGuid():N}@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.CreateAsync(
                new AppUser { UserName = email, Email = email, FirstName = "Pass", LastName = "Word", EmailConfirmed = true },
                "Password#Login1")).Succeeded);
        }

        var client = factory.CreateClient();
        var ok = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password#Login1" });
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong#Password1" });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("\"token\"", await ok.Content.ReadAsStringAsync());
        Assert.NotEqual(HttpStatusCode.OK, wrong.StatusCode);
    }
}

/// <summary>No Google client id (also whitespace-only); any outbound HTTP through the default client is recorded.</summary>
public class GoogleNotConfiguredFactory : C0WebApplicationFactory
{
    protected override void ConfigureAdditional(IWebHostBuilder builder)
    {
        builder.UseSetting("Google:ClientId", "   ");
        builder.ConfigureTestServices(services =>
            services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => new OutboundCallRecorder()));
    }
}

public class OutboundCallRecorder : HttpMessageHandler
{
    private static int _calls;
    public static int Calls => _calls;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}
