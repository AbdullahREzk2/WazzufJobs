using System.Net;
using Microsoft.AspNetCore.Hosting;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// <c>Security:DataKey</c> keys the audited client-address HMAC (Phase 02, S5). Outside Development the API refuses to
/// start without a valid key (fail closed), so an audit log without address protection can never run in production.
/// </summary>
public class AuditStartupTests
{
    private sealed class KeylessFactory(string? key, string environment = "Production") : ApiFactory
    {
        protected override void ConfigureSettings(IDictionary<string, string?> settings)
        {
            base.ConfigureSettings(settings);
            settings["Security:DataKey"] = key;
        }

        protected override void ConfigureAdditional(IWebHostBuilder builder) => builder.UseEnvironment(environment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]   // 16 bytes
    public void Production_refuses_to_start_without_a_valid_data_key(string? key)
    {
        using var factory = new KeylessFactory(key);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Security:DataKey", error.ToString());
    }

    [Fact]
    public async Task Development_starts_without_a_data_key_using_an_ephemeral_one()
    {
        await using var factory = new KeylessFactory(null, "Development");

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
