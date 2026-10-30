using System.Net;
using System.Net.Http.Json;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>S1: job seekers must not reach other applicants' data through the admin application endpoints.</summary>
public class ApplicationAuthorizationTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    [Fact]
    public async Task Candidate_cannot_read_another_candidates_application_detail()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetAsync($"/api/applications/{s.ApplicationOfB.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_read_even_their_own_application_through_the_admin_detail_endpoint()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetAsync($"/api/applications/{s.ApplicationOfA.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_list_the_applicants_of_a_job()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetAsync($"/api/applications/job/{s.Job.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_change_an_application_status()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.PutAsJsonAsync($"/api/applications/{s.ApplicationOfA.Id}/status", new { status = 4 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_download_an_applicants_cv()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetAsync($"/api/applications/{s.ApplicationOfB.Id}/cv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/applications/1")]
    [InlineData("/api/applications/job/1")]
    [InlineData("/api/applications/1/cv")]
    [InlineData("/api/users")]
    public async Task Anonymous_requests_to_admin_endpoints_are_rejected(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_still_review_applications_and_their_ai_assessment()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsAdmin(factory.Services, s.Admin);

        var list = await client.GetAsync($"/api/applications/job/{s.Job.Id}");
        var detail = await client.GetAsync($"/api/applications/{s.ApplicationOfB.Id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var names = TestData.PropertyNames(await detail.Content.ReadAsStringAsync());
        Assert.Contains("aiScore", names);
    }

    [Fact]
    public async Task Candidate_can_still_list_only_their_own_applications()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetFromJsonAsync<Page>("/api/applications/my-applications");

        Assert.NotNull(response);
        Assert.Single(response!.Items);
        Assert.Equal(s.ApplicationOfA.Id, response.Items[0].Id);
    }

    private record Page(List<Item> Items, int TotalCount);
    private record Item(int Id);
}
