using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Item 7: calls EVERY GET endpoint of the API as job seeker A and anonymously, with route values pointing at
/// another applicant's data, and asserts that no response can carry another applicant's identity, a CV storage
/// URL/key, or AI assessment data. New endpoints are covered automatically.
/// </summary>
public class LeakSweepTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    [Fact]
    public async Task No_get_endpoint_leaks_cv_urls_ai_data_or_other_applicants_data()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var forbidden = new[]
        {
            s.CandidateB.Email!, TestData.SecretFeedback, "aiScore", "aiFeedback", "isAIScored",
            "/raw/upload/", "/raw/authenticated/", "wazzuf-jobs/cvs/candidate-b", "wazzuf-jobs/cvs/candidate-a"
        };

        var urls = ExpandGetRoutes(s);
        Assert.True(urls.Count >= 15, $"expected the full API surface, found {urls.Count} GET urls");

        var candidate = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);
        var anonymous = factory.CreateClient();
        var violations = new List<string>();

        foreach (var url in urls)
        {
            foreach (var (who, client) in new[] { ("candidateA", candidate), ("anonymous", anonymous) })
            {
                var response = await client.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();
                foreach (var marker in forbidden.Where(m => body.Contains(m, StringComparison.OrdinalIgnoreCase)))
                    violations.Add($"{who} GET {url} -> {(int)response.StatusCode} contains '{marker}'");
                foreach (var header in response.Headers.Concat(response.Content.Headers))
                    if (header.Value.Any(v => v.Contains("cloudinary", StringComparison.OrdinalIgnoreCase)))
                        violations.Add($"{who} GET {url} header {header.Key} references cloudinary");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private List<string> ExpandGetRoutes(SeededScenario s)
    {
        var values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = [s.ApplicationOfB.Id.ToString(), s.Job.Id.ToString(), s.Job.CategoryId.ToString(), AppRoles.Admin.RoleId],
            ["jobId"] = [s.Job.Id.ToString()],
            ["userId"] = [s.CandidateB.Id, s.Admin.Id],
            ["roleId"] = [AppRoles.Admin.RoleId]
        };

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        var urls = new HashSet<string>();
        foreach (var endpoint in endpoints)
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            if (methods is not null && !methods.Contains("GET"))
                continue;

            var expanded = new List<string> { "/" + endpoint.RoutePattern.RawText!.TrimStart('/') };
            foreach (var parameter in endpoint.RoutePattern.Parameters)
            {
                var candidates = values.TryGetValue(parameter.Name, out var v) ? v : ["1"];
                expanded = expanded.SelectMany(u => candidates.Select(c =>
                    System.Text.RegularExpressions.Regex.Replace(u, $"{{{parameter.Name}[^}}]*}}", c))).ToList();
            }
            foreach (var url in expanded)
                urls.Add(url);
        }
        return urls.ToList();
    }
}
