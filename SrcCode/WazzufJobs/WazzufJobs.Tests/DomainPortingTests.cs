using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>Relational test host with CV uploads enabled (fake storage only).</summary>
public class SqlServerCvUploadsFactory : SqlServerApiFactory
{
    protected override bool CvUploadsEnabled => true;
}

/// <summary>A database whose platform company is missing: job creation must fail closed.</summary>
public class NoPlatformCompanyFactory : SqlServerApiFactory
{
    protected override string DatabasePrefix => "WazzufJobs_NoPlatformTest_";
    protected override void PrepareDatabase(ApplicationDBContext context)
    {
        context.Database.Migrate();
        context.Database.ExecuteSqlRaw("DELETE FROM dbo.Companies WHERE IsPlatform = 1");
    }
}

/// <summary>
/// Existing workflows on the Baseline_v2 model (Phase 02, S2b): platform-owned jobs, candidate profiles, resumes,
/// applications with company and resume taken from trusted sources, status history, tinyint enum storage behind
/// unchanged string responses, and database-enforced ownership. Throwaway SQL Server databases only.
/// </summary>
public class DomainPortingTests(SqlServerCvUploadsFactory factory) : IClassFixture<SqlServerCvUploadsFactory>
{
    private ApplicationDBContext Db() =>
        new(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(factory.ConnectionString).Options);

    private async Task<AppUser> UserAsync(string label)
    {
        await using var db = Db();
        var user = TestData.NewUser(label);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Resume> ResumeAsync(AppUser candidate, string key)
    {
        await using var db = Db();
        var resume = new Resume
        {
            CandidateId = candidate.Id, StorageKey = key, FileName = "cv.pdf", Sha256 = new byte[32], IsDefault = true,
            Text = new ResumeText { ExtractedText = "C# developer" }
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }

    private async Task<int> CreateJobAsync(AppUser admin, object? extra = null)
    {
        var response = await factory.CreateClient().AsAdmin(factory.Services, admin).PostAsJsonAsync("/api/Jobs", new
        {
            title = $"Backend {Guid.NewGuid():N}",
            description = "Build APIs",
            location = "Cairo",
            skills = new[] { "C#" },
            jobType = 1,
            workplaceType = 2,
            categoryId = 1,
            salaryMin = 1000,
            salaryMax = 2000,
            expiresAt = (DateTime?)null,
            companyId = Guid.NewGuid()          // ignored: the owner is never taken from input
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetInt32();
    }

    [SqlServerFact]
    public async Task Admin_created_jobs_belong_to_the_platform_company_and_keep_their_api_contract()
    {
        var admin = await UserAsync("admin");

        var jobId = await CreateJobAsync(admin);

        await using var db = Db();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(PlatformCompany.Id, job.CompanyId);
        Assert.NotNull(job.PublishedAt);

        // enums are stored as tinyint, the API still returns their names
        await using (var connection = new SqlConnection(factory.ConnectionString))
        {
            await connection.OpenAsync();
            var types = new SqlCommand("""
                SELECT STRING_AGG(name + ':' + TYPE_NAME(system_type_id), ',') FROM sys.columns
                WHERE object_id = OBJECT_ID('dbo.Jobs') AND name IN ('JobType','WorkplaceType','Status')
                """, connection);
            Assert.Equal(3, ((string)(await types.ExecuteScalarAsync())!).Split(',').Count(t => t.EndsWith(":tinyint")));
        }
        var json = await factory.CreateClient().AsAdmin(factory.Services, admin).GetStringAsync($"/api/Jobs/{jobId}");
        Assert.Contains("\"PartTime\"", json);
        Assert.Contains("\"Hybrid\"", json);
        Assert.Contains("\"Active\"", json);
    }

    [SqlServerFact]
    public async Task Onboarding_is_stored_in_the_candidate_profile()
    {
        var candidate = await UserAsync("onboard");
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);

        var complete = await client.PostAsJsonAsync("/api/Onboarding/complete", new
        {
            experienceYears = 4, careerLevel = 2, preferredJobTypes = new[] { 0, 1 }, preferredWorkplaceTypes = new[] { 1 },
            interestedCategoryIds = new[] { 1 }, interestedJobTitles = new[] { "Backend" }, minSalary = 1500, showSalary = true
        });
        Assert.True(complete.IsSuccessStatusCode, await complete.Content.ReadAsStringAsync());

        await using (var db = Db())
        {
            var profile = await db.CandidateProfiles.SingleAsync(p => p.UserId == candidate.Id);
            Assert.True(profile.IsProfileComplete);
            Assert.Equal(4, profile.ExperienceYears);
            Assert.Equal(CareerLevel.MidLevel, profile.CareerLevel);
            Assert.Equal([JobType.FullTime, JobType.PartTime], profile.PreferredJobTypes);
            Assert.Equal(["Backend"], profile.InterestedJobTitles);
            Assert.Equal(1500, profile.MinSalary);
        }

        var status = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/Onboarding/status");
        Assert.True(status.GetProperty("isProfileComplete").GetBoolean());
        Assert.Equal("MidLevel", status.GetProperty("careerLevel").GetString());

        var again = await client.PostAsJsonAsync("/api/Onboarding/complete", new
        {
            experienceYears = 1, careerLevel = 0, preferredJobTypes = new[] { 0 }, preferredWorkplaceTypes = new[] { 0 },
            interestedCategoryIds = Array.Empty<int>(), interestedJobTitles = Array.Empty<string>(), minSalary = (decimal?)null, showSalary = false
        });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [SqlServerFact]
    public async Task A_new_candidate_without_a_profile_reports_onboarding_incomplete()
    {
        var candidate = await UserAsync("fresh");

        var status = await factory.CreateClient().AsCandidate(factory.Services, candidate)
            .GetFromJsonAsync<System.Text.Json.JsonElement>("/api/Onboarding/status");

        Assert.False(status.GetProperty("isProfileComplete").GetBoolean());
        Assert.Equal(0, status.GetProperty("experienceYears").GetInt32());
    }

    [SqlServerFact]
    public async Task Applying_records_the_jobs_company_the_applicants_resume_and_the_status_history()
    {
        var admin = await UserAsync("admin");
        var candidate = await UserAsync("applicant");
        var resume = await ResumeAsync(candidate, $"cvs/{Guid.NewGuid():N}");
        var jobId = await CreateJobAsync(admin);
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);

        var apply = await client.PostAsync($"/api/Applications/job/{jobId}/apply", null);
        Assert.True(apply.IsSuccessStatusCode, await apply.Content.ReadAsStringAsync());

        await using (var db = Db())
        {
            var application = await db.Applications.Include(a => a.StatusHistory).SingleAsync(a => a.JobId == jobId);
            Assert.Equal(PlatformCompany.Id, application.CompanyId);
            Assert.Equal(resume.Id, application.ResumeId);
            var history = Assert.Single(application.StatusHistory);
            Assert.Null(history.FromStatus);
            Assert.Equal(ApplicationStatus.Pending, history.ToStatus);
            Assert.Equal(candidate.Id, history.ChangedById);
        }

        var duplicate = await client.PostAsync($"/api/Applications/job/{jobId}/apply", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [SqlServerFact]
    public async Task Applying_without_a_cv_is_refused()
    {
        var admin = await UserAsync("admin");
        var candidate = await UserAsync("no-cv");
        var jobId = await CreateJobAsync(admin);

        var apply = await factory.CreateClient().AsCandidate(factory.Services, candidate).PostAsync($"/api/Applications/job/{jobId}/apply", null);

        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
        Assert.Contains("Application.CVNotFound", await apply.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task Status_changes_are_recorded_with_the_acting_admin_and_returned_by_name()
    {
        var admin = await UserAsync("admin");
        var candidate = await UserAsync("applicant");
        await ResumeAsync(candidate, $"cvs/{Guid.NewGuid():N}");
        var jobId = await CreateJobAsync(admin);
        Assert.True((await factory.CreateClient().AsCandidate(factory.Services, candidate).PostAsync($"/api/Applications/job/{jobId}/apply", null)).IsSuccessStatusCode);
        int applicationId;
        await using (var db = Db())
            applicationId = (await db.Applications.SingleAsync(a => a.JobId == jobId)).Id;

        var adminClient = factory.CreateClient().AsAdmin(factory.Services, admin);
        var update = await adminClient.PutAsJsonAsync($"/api/Applications/{applicationId}/status", new { status = (int)ApplicationStatus.UnderReview });
        Assert.True(update.IsSuccessStatusCode, await update.Content.ReadAsStringAsync());

        await using (var db = Db())
        {
            var history = await db.ApplicationStatusHistory.Where(h => h.ApplicationId == applicationId).OrderBy(h => h.Id).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Equal(ApplicationStatus.Pending, history[1].FromStatus);
            Assert.Equal(ApplicationStatus.UnderReview, history[1].ToStatus);
            Assert.Equal(admin.Id, history[1].ChangedById);
        }
        Assert.Contains("\"UnderReview\"", await adminClient.GetStringAsync($"/api/Applications/{applicationId}"));
    }

    [SqlServerFact]
    public async Task Cv_upload_replaces_the_current_resume_in_place_and_deletes_the_previous_authenticated_file()
    {
        var admin = await UserAsync("admin");
        var candidate = await UserAsync("uploader");
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);
        Assert.True((await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("First CV")))).IsSuccessStatusCode);
        var jobId = await CreateJobAsync(admin);
        Assert.True((await client.PostAsync($"/api/Applications/job/{jobId}/apply", null)).IsSuccessStatusCode);

        Resume first;
        await using (var db = Db())
            first = await db.Resumes.SingleAsync(r => r.CandidateId == candidate.Id);

        Assert.True((await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("Second CV")))).IsSuccessStatusCode);

        await using (var db = Db())
        {
            var resumes = await db.Resumes.Include(r => r.Text).Where(r => r.CandidateId == candidate.Id).ToListAsync();
            var current = Assert.Single(resumes);                      // replaced in place (C0 semantics until P05)
            Assert.Equal(first.Id, current.Id);
            Assert.NotEqual(first.StorageKey, current.StorageKey);
            Assert.Equal(ResumeStorageAccess.Authenticated, current.StorageAccess);
            Assert.Contains("Second CV", current.Text!.ExtractedText);
            Assert.Equal(32, current.Sha256.Length);
            Assert.True(current.SizeBytes > 0);
        }
        Assert.Contains(factory.Storage.RawDeletes, d => d.PublicId == first.StorageKey && d.DeliveryType == CvStorage.AuthenticatedType);
    }

    [SqlServerFact]
    public async Task Deleting_a_cv_used_by_an_application_removes_the_file_and_keeps_only_a_removed_record()
    {
        var admin = await UserAsync("admin");
        var candidate = await UserAsync("deleter");
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);
        Assert.True((await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("To be deleted")))).IsSuccessStatusCode);
        var jobId = await CreateJobAsync(admin);
        Assert.True((await client.PostAsync($"/api/Applications/job/{jobId}/apply", null)).IsSuccessStatusCode);
        Resume resume;
        int applicationId;
        await using (var db = Db())
        {
            resume = await db.Resumes.SingleAsync(r => r.CandidateId == candidate.Id);
            applicationId = (await db.Applications.SingleAsync(a => a.JobId == jobId)).Id;
        }

        Assert.True((await client.DeleteAsync("/api/cv")).IsSuccessStatusCode);

        await using (var db = Db())
        {
            var removed = await db.Resumes.Include(r => r.Text).SingleAsync(r => r.Id == resume.Id);
            Assert.Equal(ResumeStatus.Removed, removed.Status);
            Assert.False(removed.IsDefault);
            Assert.Equal(string.Empty, removed.Text!.ExtractedText);
        }
        Assert.Contains(factory.Storage.RawDeletes, d => d.PublicId == resume.StorageKey && d.DeliveryType == CvStorage.AuthenticatedType);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/cv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/cv/file")).StatusCode);
        var adminView = await factory.CreateClient().AsAdmin(factory.Services, admin).GetAsync($"/api/Applications/{applicationId}/cv");
        Assert.False(adminView.IsSuccessStatusCode);

        // a new upload becomes the current resume next to the removed record
        Assert.True((await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("New CV")))).IsSuccessStatusCode);
        await using (var db = Db())
            Assert.Equal(1, await db.Resumes.CountAsync(r => r.CandidateId == candidate.Id && r.IsDefault && r.Status == ResumeStatus.Active));
    }

    [SqlServerFact]
    public async Task Deleting_an_unused_cv_deletes_the_resume_row()
    {
        var candidate = await UserAsync("unused");
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);
        Assert.True((await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("Unused CV")))).IsSuccessStatusCode);

        Assert.True((await client.DeleteAsync("/api/cv")).IsSuccessStatusCode);

        await using var db = Db();
        Assert.False(await db.Resumes.AnyAsync(r => r.CandidateId == candidate.Id));
        Assert.False(await db.ResumeTexts.AnyAsync(t => t.Resume.CandidateId == candidate.Id));
    }

    [SqlServerFact]
    public async Task Candidates_only_ever_see_their_own_cv()
    {
        var a = await UserAsync("cand-a");
        var b = await UserAsync("cand-b");
        var resumeA = await ResumeAsync(a, $"cvs/{Guid.NewGuid():N}");
        var resumeB = await ResumeAsync(b, $"cvs/{Guid.NewGuid():N}");

        var asA = await factory.CreateClient().AsCandidate(factory.Services, a).GetFromJsonAsync<System.Text.Json.JsonElement>("/api/cv");
        var asB = await factory.CreateClient().AsCandidate(factory.Services, b).GetFromJsonAsync<System.Text.Json.JsonElement>("/api/cv");

        Assert.Equal(resumeA.Id, asA.GetProperty("id").GetInt32());
        Assert.Equal(resumeB.Id, asB.GetProperty("id").GetInt32());
    }

    [SqlServerFact]
    public async Task The_database_rejects_an_application_with_another_candidates_resume_or_a_foreign_company()
    {
        var admin = await UserAsync("admin");
        var a = await UserAsync("owner-a");
        var b = await UserAsync("intruder-b");
        var resumeOfA = await ResumeAsync(a, $"cvs/{Guid.NewGuid():N}");
        var resumeOfB = await ResumeAsync(b, $"cvs/{Guid.NewGuid():N}");
        var jobId = await CreateJobAsync(admin);
        var otherCompany = new Company { Id = Guid.NewGuid(), Name = "Other", Slug = $"other-{Guid.NewGuid():N}" };
        await using (var db = Db())
        {
            db.Companies.Add(otherCompany);
            await db.SaveChangesAsync();
        }

        // B applying with A's resume
        await using (var db = Db())
        {
            db.Applications.Add(new Application { UserId = b.Id, JobId = jobId, CompanyId = PlatformCompany.Id, ResumeId = resumeOfA.Id });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // an application whose company is not the job's company
        await using (var db = Db())
        {
            db.Applications.Add(new Application { UserId = b.Id, JobId = jobId, CompanyId = otherCompany.Id, ResumeId = resumeOfB.Id });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // a job's owning company cannot be changed afterwards
        await using (var db = Db())
        {
            var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
            job.CompanyId = otherCompany.Id;   // part of the (Id, CompanyId) key that applications reference
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }
}

/// <summary>Job creation fails closed when the platform company is missing (no default or guessed owner).</summary>
public class PlatformCompanyMissingTests(NoPlatformCompanyFactory factory) : IClassFixture<NoPlatformCompanyFactory>
{
    [SqlServerFact]
    public async Task Job_creation_is_refused_and_nothing_is_stored()
    {
        var admin = TestData.NewUser("admin");
        await using (var db = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(factory.ConnectionString).Options))
        {
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().AsAdmin(factory.Services, admin).PostAsJsonAsync("/api/Jobs", new
        {
            title = "Orphan", description = "d", location = "l", skills = Array.Empty<string>(), jobType = 0, workplaceType = 0,
            categoryId = 1, salaryMin = (decimal?)null, salaryMax = (decimal?)null, expiresAt = (DateTime?)null
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Job.PlatformCompanyMissing", await response.Content.ReadAsStringAsync());
        await using var check = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(factory.ConnectionString).Options);
        Assert.False(await check.Jobs.AnyAsync());
    }
}
