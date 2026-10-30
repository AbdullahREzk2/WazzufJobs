using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WazzufJobs.DAL.Entities;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// S8: the (previously uncommitted) Google sign-in shipped with C0 must only accept Google-verified e-mails
/// and must never link to a local account whose e-mail was never confirmed. Google is replaced by a fake
/// tokeninfo endpoint; nothing external is called.
/// </summary>
public class GoogleLoginHardeningTests(GoogleEnabledFactory factory) : IClassFixture<GoogleEnabledFactory>
{
    [Fact]
    public async Task Verified_google_email_creates_and_signs_in_a_new_candidate()
    {
        var email = $"google-new-{Guid.NewGuid():N}@example.test";
        var response = await Login(FakeGoogle.Token(email, verified: "\"true\""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task Boolean_email_verified_claim_is_also_accepted()
    {
        var response = await Login(FakeGoogle.Token($"google-bool-{Guid.NewGuid():N}@example.test", verified: "true"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("\"false\"")]
    [InlineData("false")]
    [InlineData(null)]
    public async Task Unverified_google_email_is_rejected_and_no_account_is_created(string? verified)
    {
        var email = $"google-unverified-{Guid.NewGuid():N}@example.test";
        var response = await Login(FakeGoogle.Token(email, verified));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task Token_for_another_application_is_rejected()
    {
        var response = await Login(FakeGoogle.Token($"google-aud-{Guid.NewGuid():N}@example.test", "\"true\"", audience: "someone-elses-client-id"));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Google_sign_in_never_links_to_an_unconfirmed_local_account()
    {
        // attacker pre-registers the victim's e-mail with a password but never confirms it
        var email = $"victim-{Guid.NewGuid():N}@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var squatter = new AppUser { UserName = email, Email = email, FirstName = "Squat", LastName = "Ter", EmailConfirmed = false };
            Assert.True((await users.CreateAsync(squatter, "Attacker#Passw0rd!")).Succeeded);
        }

        var response = await Login(FakeGoogle.Token(email, "\"true\""));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Google_sign_in_links_to_an_existing_confirmed_account()
    {
        var email = $"confirmed-{Guid.NewGuid():N}@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.CreateAsync(new AppUser { UserName = email, Email = email, FirstName = "Real", LastName = "User", EmailConfirmed = true }, "Owner#Passw0rd!1")).Succeeded);
        }

        var response = await Login(FakeGoogle.Token(email, "\"true\""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> Login(string idToken) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/google", new { idToken });
}

public class GoogleEnabledFactory : C0WebApplicationFactory
{
    protected override void ConfigureAdditional(IWebHostBuilder builder)
    {
        builder.UseSetting("Google:ClientId", FakeGoogle.ClientId);
        builder.ConfigureTestServices(services =>
            services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => new FakeGoogle()));
    }
}

/// <summary>Fake https://oauth2.googleapis.com/tokeninfo: the id_token is a base64 JSON of the claims to return.</summary>
public class FakeGoogle : HttpMessageHandler
{
    public const string ClientId = "wazzuf-test-client.apps.googleusercontent.com";

    public static string Token(string email, string? verified, string audience = ClientId)
    {
        var verifiedJson = verified is null ? "" : $",\"email_verified\":{verified}";
        var claims = $"{{\"email\":\"{email}\",\"aud\":\"{audience}\",\"given_name\":\"G\",\"family_name\":\"User\"{verifiedJson}}}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(claims));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host != "oauth2.googleapis.com")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));

        var token = Uri.UnescapeDataString(request.RequestUri.Query.Split("id_token=")[1]);
        var claims = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(claims, Encoding.UTF8, "application/json")
        });
    }
}
