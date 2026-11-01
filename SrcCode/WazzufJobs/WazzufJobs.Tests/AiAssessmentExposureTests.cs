using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>S4: AI assessments are internal and must never reach candidates (API, email or push).</summary>
public class AiAssessmentExposureTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    private static readonly string[] ForbiddenFields = ["aiScore", "aiFeedback", "isAIScored", "score", "feedback"];

    [Fact]
    public async Task My_applications_response_contains_no_ai_assessment_fields_or_text()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetAsync("/api/applications/my-applications");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = TestData.PropertyNames(json);
        foreach (var field in ForbiddenFields)
            Assert.DoesNotContain(field, names);
        Assert.DoesNotContain(TestData.SecretFeedback, json);
    }

    [Fact]
    public void Scoring_service_has_no_way_to_notify_candidates()
    {
        var parameterTypes = typeof(AIScoringService)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(IEmailSender), parameterTypes);
        Assert.DoesNotContain(parameterTypes, t => t.Name.StartsWith("IHubContext", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scoring_still_stores_the_internal_assessment()
    {
        var options = new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase($"scoring-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDBContext(options);

        var candidate = TestData.NewUser("scored");
        var job = new DAL.Entities.Job
        {
            Title = "Dev", Description = "d", Location = "Cairo", Skills = ["C#"],
            Category = new DAL.Entities.JobCategory { Name = "IT", Slug = "it" },
            PostedBy = TestData.NewUser("poster"),
            Company = new DAL.Entities.Company { Id = Guid.NewGuid(), Name = "Co", Slug = "co" }
        };
        var resume = new DAL.Entities.Resume
        {
            Candidate = candidate, StorageKey = "p", FileName = "f.pdf", Sha256 = new byte[32], IsDefault = true,
            Text = new DAL.Entities.ResumeText { ExtractedText = "C# developer" }
        };
        var application = new DAL.Entities.Application { User = candidate, Job = job, Company = job.Company, Resume = resume };
        db.Add(application);
        await db.SaveChangesAsync();

        var service = new AIScoringService(db, new StubAiClient("SCORE: 77\nFEEDBACK: Strong C# background."), NullLogger<AIScoringService>.Instance);
        await service.ScoreApplicationAsync(application.Id, CancellationToken.None);

        var stored = await db.Applications.SingleAsync(a => a.Id == application.Id);
        Assert.True(stored.IsAIScored);
        Assert.Equal(77, stored.AIScore);
    }

    private sealed class StubAiClient(string response) : IAIClient
    {
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
