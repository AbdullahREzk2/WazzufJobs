using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using WazzufJobs.BLL.Audit;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.Tests;

/// <summary>Audit details allowlist and the client-address HMAC (Phase 02, S5).</summary>
public class AuditUnitTests
{
    // ---------------------------------------------------------------- details allowlist

    [Fact]
    public void Only_allowlisted_keys_with_plain_values_are_kept()
    {
        var json = AuditDetails.Serialize(new Dictionary<string, object?>
        {
            ["reason"] = "User.LastAdministrator",
            ["roles"] = new[] { "Candidate" },
            ["emailChanged"] = true,
            ["revokedTokens"] = 3,
            ["to"] = CompanyMemberRole.Admin,
            ["route"] = "api/Users/{userId}",
            ["password"] = "Secret#Passw0rd!",
            ["token"] = "abc",
            ["email"] = "someone@example.test",
            ["cvText"] = "C# developer",
            ["storageKey"] = "wazzuf-jobs/cvs/x"
        })!;

        var parsed = JsonDocument.Parse(json).RootElement;
        Assert.Equal(
            new[] { "dropped", "emailChanged", "reason", "revokedTokens", "roles", "route", "to" },
            parsed.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(5, parsed.GetProperty("dropped").GetInt32());
        Assert.Equal("Admin", parsed.GetProperty("to").GetString());
        foreach (var secret in new[] { "Secret#Passw0rd!", "someone@example.test", "C# developer", "wazzuf-jobs/cvs/x", "password", "token" })
            Assert.DoesNotContain(secret, json);
    }

    [Theory]
    [InlineData("someone@example.test")]                                   // email address
    [InlineData("Secret#Passw0rd!")]                                       // password-like
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJlLXNpZ25hdHVyZS1zaWduYXR1cmU")] // JWT (too long)
    [InlineData("https://res.cloudinary.com/x/raw/upload/cv.pdf")]         // URL
    [InlineData("wazzuf-jobs/cvs/candidate-a")]                            // storage key (paths only under "route")
    [InlineData("two words")]
    [InlineData("line\nbreak")]
    [InlineData("")]
    public void Unsafe_values_under_allowed_keys_are_dropped(string value)
    {
        var json = AuditDetails.Serialize(new Dictionary<string, object?> { ["reason"] = value, ["roles"] = new[] { "Candidate", value } })!;

        Assert.Equal("{\"dropped\":2}", json);
    }

    [Fact]
    public void Lists_longer_than_ten_and_unsupported_types_are_dropped_and_empty_details_are_null()
    {
        Assert.Equal("{\"dropped\":2}", AuditDetails.Serialize(new Dictionary<string, object?>
        {
            ["roles"] = Enumerable.Range(0, 11).Select(i => $"r{i}").ToArray(),
            ["reason"] = new { nested = "object" }
        }));
        Assert.Null(AuditDetails.Serialize(null));
        Assert.Null(AuditDetails.Serialize(new Dictionary<string, object?>()));
        Assert.Equal("{\"dropped\":1}", AuditDetails.Serialize(new Dictionary<string, object?> { ["reason"] = null }));
    }

    [Fact]
    public void Only_route_may_hold_an_endpoint_template_and_never_a_url()
    {
        Assert.Equal("{\"route\":\"api/Users/{userId}\"}", AuditDetails.Serialize(new Dictionary<string, object?> { ["route"] = "api/Users/{userId}" }));
        Assert.Equal("{\"dropped\":1}", AuditDetails.Serialize(new Dictionary<string, object?> { ["route"] = "https://evil.example/x" }));
        Assert.Equal("{\"dropped\":1}", AuditDetails.Serialize(new Dictionary<string, object?> { ["reason"] = "api/Users/{userId}" }));
    }

    [Fact]
    public void Every_audit_action_is_a_plain_unique_name()
    {
        Assert.True(AuditActions.All.Count >= 30);
        Assert.Equal(AuditActions.All.Count, AuditActions.All.Distinct().Count());
        Assert.All(AuditActions.All, a => Assert.Matches("^[a-z_]+(\\.[a-z_]+)+$", a));
    }

    [Fact]
    public void Success_events_are_required_and_failures_and_denials_are_not()
    {
        Assert.True(AuditEvent.Success("x.y").Required);
        Assert.False(AuditEvent.Failure("x.y").Required);
        Assert.False(AuditEvent.Denied("x.y").Required);
    }

    // ---------------------------------------------------------------- IP HMAC

    private static readonly byte[] KeyA = Enumerable.Repeat((byte)7, 32).ToArray();
    private static readonly byte[] KeyB = Enumerable.Repeat((byte)8, 32).ToArray();

    [Fact]
    public void Addresses_are_hashed_with_a_keyed_hmac_never_stored_in_plaintext()
    {
        var hasher = new HmacIpHasher(KeyA);
        var address = IPAddress.Parse("203.0.113.7");

        var hash = hasher.Hash(address)!;

        Assert.Equal(32, hash.Length);
        Assert.Equal(hash, hasher.Hash(IPAddress.Parse("203.0.113.7")));
        Assert.Equal(hash, hasher.Hash(IPAddress.Parse("::ffff:203.0.113.7")));
        Assert.NotEqual(hash, hasher.Hash(IPAddress.Parse("203.0.113.8")));
        Assert.NotEqual(hash, new HmacIpHasher(KeyB).Hash(address));
        Assert.NotEqual(hash, SHA256.HashData(address.GetAddressBytes()));   // keyed, not a plain hash
        Assert.Equal(-1, hash.AsSpan().IndexOf(address.GetAddressBytes()));
        Assert.Null(hasher.Hash(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void The_hasher_refuses_keys_that_are_not_32_bytes(int length)
    {
        Assert.Throws<ArgumentException>(() => new HmacIpHasher(new byte[length]));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("not base64!", false)]
    [InlineData("AAAA", false)]                                            // 3 bytes
    [InlineData("d2F6enVmam9icy10ZXN0LW9ubHktYXVkaXQta2V5ISE=", true)]     // 32 bytes
    public void Security_options_accept_only_a_32_byte_base64_key(string? value, bool valid)
    {
        Assert.Equal(valid, new SecurityOptions { DataKey = value }.TryDecodeDataKey() is { Length: 32 });
    }
}
