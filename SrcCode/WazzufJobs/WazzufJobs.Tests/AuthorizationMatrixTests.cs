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
/// errors); "-" means it is refused there (401 anonymous, 403 authenticated); "M" means company membership decides
/// (S4): the persona is signed in but not a member of the requested company, so the answer must be 404 Company.NotFound.
/// Tenant isolation itself is covered by <see cref="CompanyAccessTests"/>.
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

        // company module (S4): no platform permission; only an active membership in the route's company grants access
        ["GET /api/v1/companies/{companyId:guid}"] =         "-MMMMM",
        ["GET /api/v1/companies/{companyId:guid}/members"] = "-MMMMM",

        // the caller's own sessions (S6a): any signed-in account, own sessions only
        ["GET /api/v1/me/sessions"] =                        "-YYYYY",
        ["DELETE /api/v1/me/sessions/current"] =             "-YYYYY",
        ["DELETE /api/v1/me/sessions/{sessionId:guid}"] =    "-YYYYY",
        ["POST /api/v1/me/sessions/revoke-others"] =         "-YYYYY",
    };

    [Fact]
    public void Every_endpoint_has_a_matrix_entry_and_every_entry_an_endpoint()
    {
        var endpoints = Endpoints().Select(e => e.Key).ToHashSet();

        var missing = endpoints.Except(Matrix.Keys).Order().ToList();
        var stale = Matrix.Keys.Except(endpoints).Order().ToList();

        Assert.True(missing.Count == 0, "Endpoints without an authorization-matrix entry: " + string.Join(", ", missing));
        Assert.True(stale.Count == 0, "Matrix entries without an endpoint: " + string.Join(", ", stale));
        Assert.All(Matrix.Values, row => Assert.Matches("^[YM-]{6}$", row));
        Assert.All(Matrix.Values.Where(row => row.Contains('M')), row => Assert.Equal('-', row[0]));
    }

    [Fact]
    public async Task Every_endpoint_enforces_its_matrix_row_for_every_persona()
    {
        // a fresh session per request: some endpoints end the caller's session (S6a)
        var users = new Dictionary<string, (Persona Persona, AppUser User)>();
        foreach (var persona in TestPersonas.All)
            users[persona.Name] = (persona, await CreateUserAsync(persona));
        HttpClient ClientFor(string column) => column == "anonymous"
            ? factory.CreateClient()
            : factory.CreateClient().As(factory.Services, users[column].Persona, users[column].User);

        var violations = new List<string>();
        foreach (var (key, template, multipart) in Endpoints())
        {
            var row = Matrix[key];
            for (var column = 0; column < Columns.Length; column++)
            {
                var (status, body) = await SendAsync(ClientFor(Columns[column]), key, template, multipart);
                var refused = status == HttpStatusCode.Unauthorized || (status == HttpStatusCode.Forbidden && body.Length == 0);
                if (row[column] == 'M')
                {
                    if (status != HttpStatusCode.NotFound || !body.Contains("Company.NotFound"))
                        violations.Add($"{key} as {Columns[column]}: expected 404 Company.NotFound (not a member), got {(int)status}");
                    continue;
                }
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
        var url = Regex.Replace(template, "{(\\w+)[^}]*}", m => m.Groups[1].Value is "userId" or "roleId" or "companyId" or "sessionId"
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
