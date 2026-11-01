using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>S3/B15 with uploads disabled (the default C0 state).</summary>
public class CvProtectionTests(C0WebApplicationFactory factory) : IClassFixture<C0WebApplicationFactory>
{
    [Fact]
    public async Task Cv_upload_is_refused_while_uploads_are_disabled_and_nothing_is_stored()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);
        var before = factory.Storage.PrivateUploads.Count;

        var response = await client.PostAsync("/api/cv", PdfForm(PdfBuilder.CreateTextPdf("Synthetic CV")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(before, factory.Storage.PrivateUploads.Count);
    }

    [Fact]
    public async Task Cv_metadata_never_contains_a_storage_url()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var json = await client.GetStringAsync("/api/cv");

        Assert.DoesNotContain("url", TestData.PropertyNames(json));
        Assert.DoesNotContain("cloudinary", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Candidate_downloads_own_cv_through_the_api_with_safe_headers()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.GetAsync("/api/cv/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains(factory.Storage.Downloads, d => d.PublicId == s.CvOfA.StorageKey && d.DeliveryType == CvStorage.AuthenticatedType);
    }

    [Fact]
    public async Task Admin_views_an_applicants_cv_through_the_api_not_a_public_url()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsAdmin(factory.Services, s.Admin);

        var detailJson = await client.GetStringAsync($"/api/applications/{s.ApplicationOfB.Id}");
        var file = await client.GetAsync($"/api/applications/{s.ApplicationOfB.Id}/cv");

        Assert.DoesNotContain("cvUrl", TestData.PropertyNames(detailJson));
        Assert.DoesNotContain("cloudinary", detailJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        // legacy public asset of candidate B is fetched server-side via a signed API download
        Assert.Contains(factory.Storage.Downloads, d => d.PublicId == "wazzuf-jobs/cvs/candidate-b" && d.DeliveryType == CvStorage.PublicUploadType);
    }

    [Fact]
    public async Task Admin_user_details_do_not_contain_a_cv_url()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsAdmin(factory.Services, s.Admin);

        var json = await client.GetStringAsync($"/api/users/{s.CandidateA.Id}");

        Assert.DoesNotContain("cvUrl", TestData.PropertyNames(json));
        Assert.DoesNotContain("cloudinary.com/test/raw", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deleting_a_legacy_public_cv_retains_the_asset_for_approved_remediation()
    {
        var s = await TestData.SeedAsync(factory.Services, legacyPublicCvForA: true);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.DeleteAsync("/api/cv");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(factory.Storage.RawDeletes, d => d.PublicId == s.CvOfA.StorageKey);
    }

    internal static MultipartFormDataContent PdfForm(byte[] bytes, string contentType = "application/pdf", string fileName = "my cv.pdf")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}

/// <summary>Behaviour once uploads are re-enabled after the Cloudinary verification passes.</summary>
public class CvUploadsEnabledTests(CvUploadsEnabledFactory factory) : IClassFixture<CvUploadsEnabledFactory>
{
    [Fact]
    public async Task Uploaded_cv_is_stored_as_an_authenticated_asset_and_no_url_is_returned()
    {
        var candidate = await SeedCandidateAsync();
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);

        var response = await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("Synthetic CV for upload test")));
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("url", TestData.PropertyNames(json));
        using var scope = factory.Services.CreateScope();
        var cv = await scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().Resumes.SingleAsync(c => c.CandidateId == candidate.Id);
        Assert.Equal(CvStorage.AuthenticatedType, CvStorage.DeliveryType(cv.StorageAccess));
        Assert.Contains(cv.StorageKey, factory.Storage.PrivateUploads);
    }

    [Fact]
    public async Task File_that_only_claims_to_be_a_pdf_is_rejected()
    {
        var candidate = await SeedCandidateAsync();
        var client = factory.CreateClient().AsCandidate(factory.Services, candidate);

        var response = await client.PostAsync("/api/cv", CvProtectionTests.PdfForm("MZ this is not a pdf"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replacing_a_legacy_public_cv_keeps_the_legacy_asset_untouched()
    {
        var s = await TestData.SeedAsync(factory.Services, legacyPublicCvForA: true);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("Replacement CV")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.Storage.RawDeletes, d => d.PublicId == s.CvOfA.StorageKey);
    }

    [Fact]
    public async Task Replacing_an_authenticated_cv_deletes_the_previous_raw_asset_explicitly()
    {
        var s = await TestData.SeedAsync(factory.Services);
        var client = factory.CreateClient().AsCandidate(factory.Services, s.CandidateA);

        var response = await client.PostAsync("/api/cv", CvProtectionTests.PdfForm(PdfBuilder.CreateTextPdf("Replacement CV")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(factory.Storage.RawDeletes, d => d.PublicId == s.CvOfA.StorageKey && d.DeliveryType == CvStorage.AuthenticatedType);
    }

    private async Task<DAL.Entities.AppUser> SeedCandidateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var user = TestData.NewUser("uploader");
        db.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
