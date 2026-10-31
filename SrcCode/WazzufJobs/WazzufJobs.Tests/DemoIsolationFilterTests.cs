using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Demo-isolation query filters (Phase 02, S2a; architecture §8). Demo functionality is not enabled: every request uses
/// the real partition. These tests seed one real and one demo record of every filtered entity and check each scope.
/// </summary>
public class DemoIsolationFilterTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    private sealed class Scope(DataScopeMode mode, bool includePublicDemo = false) : IDataScope
    {
        public DataScopeMode Mode => mode;
        public bool IncludePublicDemo => includePublicDemo;
    }

    private ApplicationDBContext Context(IDataScope? scope = null) =>
        new(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(factory.ConnectionString).Options, scope);

    private sealed record Seeded(string Marker, Guid RealCompany, Guid DemoCompany);

    private async Task<Seeded> SeedPartitionsAsync()
    {
        var marker = Guid.NewGuid().ToString("N")[..12];
        await using var db = Context(new Scope(DataScopeMode.All));

        Guid AddGraph(bool demo)
        {
            var user = TestData.NewUser(demo ? "demo" : "real");
            user.IsDemo = demo;
            var company = new Company { Id = Guid.NewGuid(), Name = $"{marker}-{demo}", Slug = $"{marker}-{demo}", IsDemo = demo };
            var category = new JobCategory { Name = $"{marker}-{demo}", Slug = $"{marker}-{demo}" };
            var job = new Job
            {
                Title = marker, Description = "d", Location = "l", Category = category, PostedBy = user, Company = company, IsDemo = demo
            };
            var resume = new Resume
            {
                Candidate = user, FileName = "cv.pdf", StorageKey = $"cvs/{marker}-{demo}", Sha256 = new byte[32], IsDemo = demo,
                Text = new ResumeText { ExtractedText = marker }
            };
            var application = new Application { User = user, Job = job, Company = company, Resume = resume, IsDemo = demo };
            db.AddRange(user, company, category, job, resume, application,
                new CompanyMember { Company = company, User = user, Role = CompanyMemberRole.Owner },
                new CandidateProfile { User = user, Headline = marker, IsDemo = demo },
                new SavedJob { User = user, Job = job, IsDemo = demo },
                new ApplicationStatusHistory { Application = application, ToStatus = ApplicationStatus.Pending, Reason = marker });
            return company.Id;
        }

        var real = AddGraph(demo: false);
        var demo = AddGraph(demo: true);
        await db.SaveChangesAsync();
        return new Seeded(marker, real, demo);
    }

    private static async Task<Dictionary<string, int>> CountsAsync(ApplicationDBContext db, string marker) => new()
    {
        ["companies"] = await db.Companies.CountAsync(c => c.Name.StartsWith(marker)),
        ["jobs"] = await db.Jobs.CountAsync(j => j.Title == marker),
        ["members"] = await db.CompanyMembers.CountAsync(m => m.Company.Name.StartsWith(marker)),
        ["applications"] = await db.Applications.CountAsync(a => a.Job.Title == marker),
        ["history"] = await db.ApplicationStatusHistory.CountAsync(h => h.Reason == marker),
        ["resumes"] = await db.Resumes.CountAsync(r => r.StorageKey.StartsWith("cvs/" + marker)),
        ["resumeTexts"] = await db.ResumeTexts.CountAsync(t => t.ExtractedText == marker),
        ["profiles"] = await db.CandidateProfiles.CountAsync(p => p.Headline == marker),
        ["savedJobs"] = await db.SavedJobs.CountAsync(s => s.Job.Title == marker)
    };

    [SqlServerFact]
    public async Task Default_scope_sees_only_real_data()
    {
        var s = await SeedPartitionsAsync();
        await using var db = Context();   // no scope supplied: DefaultDataScope (real)

        Assert.All(await CountsAsync(db, s.Marker), c => Assert.True(c.Value == 1, $"{c.Key}: {c.Value}"));
        Assert.True(await db.Companies.AnyAsync(c => c.Id == s.RealCompany));
        Assert.False(await db.Companies.AnyAsync(c => c.Id == s.DemoCompany));
    }

    [SqlServerFact]
    public async Task Demo_scope_sees_only_demo_data()
    {
        var s = await SeedPartitionsAsync();
        await using var db = Context(new Scope(DataScopeMode.Demo));

        Assert.All(await CountsAsync(db, s.Marker), c => Assert.True(c.Value == 1, $"{c.Key}: {c.Value}"));
        Assert.True(await db.Companies.AnyAsync(c => c.Id == s.DemoCompany));
        Assert.False(await db.Companies.AnyAsync(c => c.Id == s.RealCompany));
    }

    [SqlServerFact]
    public async Task All_scope_sees_both_partitions()
    {
        var s = await SeedPartitionsAsync();
        await using var db = Context(new Scope(DataScopeMode.All));

        Assert.All(await CountsAsync(db, s.Marker), c => Assert.True(c.Value == 2, $"{c.Key}: {c.Value}"));
    }

    [SqlServerFact]
    public async Task Public_demo_listing_adds_only_demo_jobs_and_companies()
    {
        var s = await SeedPartitionsAsync();
        await using var db = Context(new Scope(DataScopeMode.Real, includePublicDemo: true));

        var counts = await CountsAsync(db, s.Marker);
        Assert.Equal(2, counts["jobs"]);
        Assert.Equal(2, counts["companies"]);
        foreach (var key in new[] { "members", "applications", "history", "resumes", "resumeTexts", "profiles", "savedJobs" })
            Assert.True(counts[key] == 1, $"{key}: {counts[key]}");
    }

    [Fact]
    public void Requests_use_the_real_partition_until_demo_is_enabled()
    {
        using var scope = factory.Services.CreateScope();
        var dataScope = scope.ServiceProvider.GetRequiredService<IDataScope>();

        Assert.Equal(DataScopeMode.Real, dataScope.Mode);
        Assert.False(dataScope.IncludePublicDemo);
    }

    [Fact]
    public void No_production_code_bypasses_the_demo_filters()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "WazzufJobs.BLL")))
            root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("source root not found");

        // allow-list: none yet (S9 adds the audited staff call sites)
        var offenders = new[] { "WazzufJobs.API", "WazzufJobs.BLL", "WazzufJobs.DAL" }
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"\.IgnoreQueryFilters\s*\("))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
