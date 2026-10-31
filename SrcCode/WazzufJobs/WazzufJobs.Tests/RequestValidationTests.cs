using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WazzufJobs.BLL.Errors;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// End-to-end behaviour of automatic request validation (Phase 02, S1): RFC 9457 400 responses with field-level
/// errors, no echo of submitted values, authorization before validation, and untouched requests without validators.
/// </summary>
public class RequestValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> ReadProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Invalid_registration_returns_one_problem_with_every_field_error()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/Auth/Register",
            new { email = "", password = "", firstName = "", lastName = "" });

        var problem = await ReadProblem(response);
        Assert.Equal(ValidationErrors.Title, problem.GetProperty("title").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(ValidationErrors.Code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.True(problem.TryGetProperty("traceId", out _));

        var fields = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Equal(["email", "firstName", "lastName", "password"], fields.Order());
    }

    [Fact]
    public async Task Validation_errors_never_echo_submitted_values_or_internal_details()
    {
        const string passwordSentinel = "sentinel-weak-password";
        const string emailSentinel = "sentinel-not-an-email";

        var response = await factory.CreateClient().PostAsJsonAsync("/api/Auth/Register",
            new { email = emailSentinel, password = passwordSentinel, firstName = new string('x', 101), lastName = "Test" });

        var body = (await ReadProblem(response)).GetRawText();
        Assert.DoesNotContain(passwordSentinel, body);
        Assert.DoesNotContain(emailSentinel, body);
        Assert.DoesNotContain(new string('x', 101), body);
        Assert.DoesNotContain("requestDTO", body);                 // no internal command structure
        Assert.DoesNotContain("attemptedValue", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valid_registration_is_not_affected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/Auth/Register",
            new { email = $"valid-{Guid.NewGuid():N}@example.test", password = "Valid#Passw0rd", firstName = "Al", lastName = "Li" });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Malformed_login_is_a_validation_error()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/Auth/login", new { email = "not-an-email", password = "" });

        var errors = (await ReadProblem(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Well_formed_login_with_wrong_credentials_is_still_an_authentication_failure()
    {
        // a short password must not turn into a validation error: credential checks stay with Identity (401)
        var response = await factory.CreateClient().PostAsJsonAsync("/api/Auth/login",
            new { email = $"unknown-{Guid.NewGuid():N}@example.test", password = "abc" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Requests_without_a_validator_are_not_affected()
    {
        var s = await TestData.SeedAsync(factory.Services);

        var response = await factory.CreateClient().AsCandidate(factory.Services, s.CandidateA).GetAsync("/api/Applications/my-applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_runs_before_validation()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var anonymous = factory.CreateClient();
        var candidate = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        // anonymous: 401, not 400, even with an invalid body
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync("/api/Account/change-password", new { currentPassword = "", newPassword = "" })).StatusCode);

        // authenticated without permission: 403, and no validation details of admin endpoints are revealed
        var updateUser = await candidate.PutAsJsonAsync($"/api/Users/{s.CandidateB.Id}", new { firstName = "", lastName = "", email = "x", roles = (string[]?)null });
        Assert.Equal(HttpStatusCode.Forbidden, updateUser.StatusCode);
        var createJob = await candidate.PostAsJsonAsync("/api/Jobs", new { title = "", description = "" });
        Assert.Equal(HttpStatusCode.Forbidden, createJob.StatusCode);
    }

    [Fact]
    public async Task Changing_to_the_same_password_is_rejected_without_revealing_it()
    {
        // documented S1 behaviour change: the existing rule now runs
        const string password = "Same#Passw0rd-sentinel";
        var s = await TestData.SeedAsync(factory.Services);

        var response = await factory.CreateClient().AsCandidate(factory.Services, s.CandidateA)
            .PutAsJsonAsync("/api/Account/change-password", new { currentPassword = password, newPassword = password });

        var problem = await ReadProblem(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty("newPassword", out _));
        Assert.DoesNotContain(password, problem.GetRawText());
    }
}
