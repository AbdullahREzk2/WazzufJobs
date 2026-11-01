using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Invariant P02-1 (Phase 02, S3): every endpoint has an explicit, reviewed authorization entry for every persona.
/// An endpoint without an entry, or an entry without an endpoint, fails the build. Each cell is exercised over HTTP:
/// "Y" means the request passes the authentication and authorization layer (any other status, including business
/// errors); "-" means it is refused there (401 anonymous, 403 authenticated).
/// </summary>
public class AuthorizationMatrixTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    // Columns:                     anonymous  SuperAdmin  Moderator  Candidate  CompanyUser  DemoAdmin
    private static readonly string[] Columns = ["anonymous", "superadmin", "moderator", "candidate", "companyuser", "demoadmin"];

    private static readonly Dictionary<string, string> Matrix = new()
    {
        // public
        ["* /health"] =                                      "YYYYYY",
        ["POST /api/Auth/login"] =                           "YYYYYY",
        ["POST /api/Auth/refresh"] =                         "YYYYYY",
        ["POST /api/Auth/revoke-refresh-token"] =            "YYYYYY",
        ["POST /api/Auth/Register"] =                        "YYYYYY",
        ["GET /api/Auth/Confirm-Email"] =                    "YYYYYY",
        ["POST /api/Auth/Resend-Confirm-Email"] =            "YYYYYY",
        ["POST /api/Auth/forget-Password"] =                 "YYYYYY",
        ["POST /api/Auth/Reset-Password"] =                  "YYYYYY",
        ["POST /api/Auth/google"] =                          "YYYYYY",

        // any signed-in account, own data only
        ["GET /api/Account/userInfo"] =                      "-YYYYY",
        ["PUT /api/Account/update-User-Info"] =              "-YYYYY",
        ["PUT /api/Account/change-password"] =               "-YYYYY",
        ["POST /api/Account/profile-image"] =                "-YYYYY",
        ["GET /api/Onboarding/status"] =                     "-YYYYY",
        ["POST /api/Onboarding/complete"] =                  "-YYYYY",
        ["GET /api/Jobs"] =                                  "-YYYYY",
        ["GET /api/Jobs/{id}"] =                             "-YYYYY",
        ["* /hubs/scoring/negotiate"] =                      "-YYYYY",
        ["* /hubs/scoring"] =                                "-YYYYY",

        // reference data
        ["GET /api/Categories"] =                            "-YYY-Y",
        ["GET /api/Categories/{id}"] =                       "-YYY-Y",
        ["POST /api/Categories"] =                           "-Y----",
        ["PUT /api/Categories/{id}"] =                       "-Y----",
        ["DELETE /api/Categories/{id}"] =                    "-Y----",

        // job administration (platform company until P04)
        ["POST /api/Jobs"] =                                 "-Y----",
        ["PUT /api/Jobs/{id}"] =                             "-Y----",
        ["DELETE /api/Jobs/{id}"] =                          "-Y----",
        ["PUT /api/Jobs/{id}/toggle-status"] =               "-Y----",

        // candidate self-service
        ["POST /api/Applications/job/{jobId}/apply"] =       "-Y-Y--",
        ["GET /api/Applications/my-applications"] =          "-Y-Y--",
        ["GET /api/CV"] =                                    "-Y-Y--",
        ["GET /api/CV/file"] =                               "-Y-Y--",
        ["POST /api/CV"] =                                   "-Y-Y--",
        ["DELETE /api/CV"] =                                 "-Y-Y--",
        ["GET /api/SavedJobs"] =                             "-Y-Y--",
        ["POST /api/SavedJobs/{jobId}"] =                    "-Y-Y--",
        ["DELETE /api/SavedJobs/{jobId}"] =                  "-Y-Y--",

        // applicants, CVs and AI assessments: Super Admin only (C0 gate, until P08)
        ["GET /api/Applications/job/{jobId}"] =              "-Y----",
        ["GET /api/Applications/{id}"] =                     "-Y----",
        ["PUT /api/Applications/{id}/status"] =              "-Y----",
        ["GET /api/Applications/{id}/cv"] =                  "-Y----",

        // users: the list is read-only for moderators; changes are Super Admin only
        ["GET /api/Users"] =                                 "-YY---",
        ["GET /api/Users/{userId}"] =                        "-YY---",
        ["POST /api/Users"] =                                "-Y----",
        ["PUT /api/Users/{userId}"] =                        "-Y----",
        ["PUT /api/Users/{userId}/toggle-status"] =          "-Y----",
        ["PUT /api/Users/{userId}/unlock"] =                 "-Y----",

        // roles: read-only; mutations pass authorization only to answer 403 Roles.Fixed
        ["GET /api/Roles"] =                                 "-Y----",
        ["GET /api/Roles/{id}"] =                            "-Y----",
        ["POST /api/Roles"] =                                "-Y----",
        ["PUT /api/Roles/{roleId}"] =                        "-Y----",
        ["PUT /api/Roles/{roleId}/toggle-status"] =          "-Y----",
    };

    [Fact]
    public void Every_endpoint_has_a_matrix_entry_and_every_entry_an_endpoint()
    {
        var endpoints = Endpoints().Select(e => e.Key).ToHashSet();

        var missing = endpoints.Except(Matrix.Keys).Order().ToList();
        var stale = Matrix.Keys.Except(endpoints).Order().ToList();

        Assert.True(missing.Count == 0, "Endpoints without an authorization-matrix entry: " + string.Join(", ", missing));
        Assert.True(stale.Count == 0, "Matrix entries without an endpoint: " + string.Join(", ", stale));
        Assert.All(Matrix.Values, row => Assert.Matches("^[Y-]{6}$", row));
    }

    [Fact]
    public async Task Every_endpoint_enforces_its_matrix_row_for_every_persona()
    {
        var clients = new Dictionary<string, HttpClient> { ["anonymous"] = factory.CreateClient() };
        foreach (var persona in TestPersonas.All)
            clients[persona.Name] = factory.CreateClient().As(factory.Services, persona, await CreateUserAsync(persona));

        var violations = new List<string>();
        foreach (var (key, template, multipart) in Endpoints())
        {
            var row = Matrix[key];
            for (var column = 0; column < Columns.Length; column++)
            {
                var (status, body) = await SendAsync(clients[Columns[column]], key, template, multipart);
                var refused = status == HttpStatusCode.Unauthorized || (status == HttpStatusCode.Forbidden && body.Length == 0);
                var expectedAllowed = row[column] == 'Y';
                if (expectedAllowed == refused)
                    violations.Add($"{key} as {Columns[column]}: expected {(expectedAllowed ? "allowed" : "refused")}, got {(int)status}");
                if (!expectedAllowed && Columns[column] == "anonymous" && status != HttpStatusCode.Unauthorized)
                    violations.Add($"{key} as anonymous: expected 401, got {(int)status}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Staff_columns_never_grant_applicant_cv_or_ai_endpoints_to_moderators_or_demo_admins()
    {
        var sensitive = Matrix.Keys.Where(k => k.Contains("/api/Applications") || k.Contains("/api/CV") || k.StartsWith("POST /api/Users")
            || k.StartsWith("PUT /api/Users") || k.Contains("/api/Roles") || k.Contains("/api/SavedJobs")).ToList();

        Assert.NotEmpty(sensitive);
        Assert.All(sensitive, k => Assert.Equal('-', Matrix[k][Array.IndexOf(Columns, "moderator")]));
        Assert.All(sensitive, k => Assert.Equal('-', Matrix[k][Array.IndexOf(Columns, "demoadmin")]));
        Assert.All(Matrix.Keys.Where(k => k.Contains("/api/Users")), k => Assert.Equal('-', Matrix[k][Array.IndexOf(Columns, "demoadmin")]));
    }

    // multipart endpoints are selected by content type during routing (415 otherwise), so send the declared type
    private List<(string Key, string Template, bool Multipart)> Endpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(e =>
            {
                var template = "/" + e.RoutePattern.RawText!.TrimStart('/');
                var methods = e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                var multipart = e.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes.Contains("multipart/form-data") == true;
                return methods is null || methods.Count == 0
                    ? [($"* {template}", template, multipart)]
                    : methods.Select(m => ($"{m} {template}", template, multipart));
            })
            .Distinct()
            .ToList();

    private static async Task<(HttpStatusCode, string)> SendAsync(HttpClient client, string key, string template, bool multipart)
    {
        var method = key.Split(' ')[0];
        var url = Regex.Replace(template, "{(\\w+)[^}]*}", m => m.Groups[1].Value == "userId" || m.Groups[1].Value == "roleId"
            ? Guid.NewGuid().ToString()
            : "999999");

        var httpMethod = method != "*" ? method : url.EndsWith("/negotiate") ? "POST" : "GET";
        using var request = new HttpRequestMessage(new HttpMethod(httpMethod), url);
        if (method is "POST" or "PUT")
            request.Content = multipart
                ? new MultipartFormDataContent { { new StringContent("x"), "unused" } }
                : new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<AppUser> CreateUserAsync(Persona persona)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = persona.NewUser();
        Assert.True((await users.CreateAsync(user, "Matrix#Passw0rd!1")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, persona.Role)).Succeeded);
        return user;
    }
}
