using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.Tests.Infrastructure;

public record SeededScenario(
    AppUser Admin,
    AppUser CandidateA,
    AppUser CandidateB,
    Job Job,
    Application ApplicationOfA,
    Application ApplicationOfB,
    CV CvOfA);

public static class TestData
{
    public const string SecretFeedback = "INTERNAL-ASSESSMENT-TEXT-MUST-NOT-LEAK";

    public static AppUser NewUser(string label) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = $"{label}-{Guid.NewGuid():N}@example.test",
        NormalizedUserName = $"{label}-{Guid.NewGuid():N}@EXAMPLE.TEST",
        Email = $"{label}-{Guid.NewGuid():N}@example.test",
        FirstName = label,
        LastName = "Test",
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    /// <summary>Two candidates who applied to the same job, both already AI-scored, plus an admin.</summary>
    public static async Task<SeededScenario> SeedAsync(IServiceProvider services, string? legacyCvUrlForA = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        var admin = NewUser("admin");
        var candidateA = NewUser("candidateA");
        var candidateB = NewUser("candidateB");

        var category = new JobCategory { Name = "Software", Slug = $"software-{Guid.NewGuid():N}" };
        var job = new Job
        {
            Title = "Backend Developer",
            Description = "Build APIs",
            Location = "Cairo",
            Skills = ["C#"],
            Category = category,
            PostedBy = admin,
            Status = JobStatus.Active
        };

        var cvA = new CV
        {
            User = candidateA,
            PublicId = "wazzuf-jobs/cvs/candidate-a",
            Url = legacyCvUrlForA ?? "https://res.cloudinary.com/test/raw/authenticated/s--sig--/v1/wazzuf-jobs/cvs/candidate-a",
            FileName = "candidate-a.pdf",
            ExtractedText = "C# developer"
        };
        var cvB = new CV
        {
            User = candidateB,
            PublicId = "wazzuf-jobs/cvs/candidate-b",
            Url = "https://res.cloudinary.com/test/raw/upload/v1/wazzuf-jobs/cvs/candidate-b",
            FileName = "candidate-b.pdf",
            ExtractedText = "Java developer"
        };

        var applicationA = new Application { User = candidateA, Job = job, AIScore = 91, AIFeedback = SecretFeedback, IsAIScored = true };
        var applicationB = new Application { User = candidateB, Job = job, AIScore = 42, AIFeedback = SecretFeedback, IsAIScored = true };

        db.AddRange(admin, candidateA, candidateB, category, job, cvA, cvB, applicationA, applicationB);
        await db.SaveChangesAsync();

        return new SeededScenario(admin, candidateA, candidateB, job, applicationA, applicationB, cvA);
    }

    public static HttpClient AsAdmin(this HttpClient client, IServiceProvider services, AppUser admin) =>
        client.As(services, TestPersonas.Admin, admin);

    public static HttpClient AsCandidate(this HttpClient client, IServiceProvider services, AppUser candidate) =>
        client.As(services, TestPersonas.Candidate, candidate);

    /// <summary>Collects every JSON property name (any depth, lower-cased) so tests can assert a field is absent.</summary>
    public static HashSet<string> PropertyNames(string json)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject()) { names.Add(p.Name); Walk(p.Value); }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in e.EnumerateArray()) Walk(item);
                    break;
            }
        }
        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement);
        return names;
    }
}
