using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;
using WazzufJobs.DAL.Enums;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

public class StorageUnitTests
{
    [Fact]
    public void Cv_upload_parameters_are_always_authenticated_and_never_use_the_client_filename()
    {
        using var content = new MemoryStream(PdfBuilder.CreateTextPdf("x"));

        var parameters = CloudinaryDocumentStorage.BuildPrivateRawUploadParams(content, CvStorage.CvFolder);

        Assert.Equal("authenticated", parameters.Type);
        Assert.False(parameters.UseFilename);
        Assert.False(parameters.Overwrite);
        Assert.Equal(CvStorage.CvFolder, parameters.Folder);
        Assert.Equal("cv.pdf", parameters.File.FileName);
    }

    [Theory]
    [InlineData(ResumeStorageAccess.Authenticated, "authenticated", false)]
    [InlineData(ResumeStorageAccess.LegacyPublic, "upload", true)]
    public void Delivery_type_is_derived_from_the_stored_access_type(ResumeStorageAccess access, string expected, bool legacyPublic)
    {
        // Phase 02 S2b: the access type is stored with the resume; no storage URL is kept
        Assert.Equal(expected, CvStorage.DeliveryType(access));
        Assert.Equal(legacyPublic, CvStorage.IsLegacyPublic(access));
    }

    [Fact]
    public void Pdf_signature_check_rejects_non_pdf_content()
    {
        Assert.True(CvStorage.HasPdfSignature(PdfBuilder.CreateTextPdf("x")));
        Assert.False(CvStorage.HasPdfSignature("MZ\u0090\u0000"u8));
        Assert.False(CvStorage.HasPdfSignature(ReadOnlySpan<byte>.Empty));
    }
}
