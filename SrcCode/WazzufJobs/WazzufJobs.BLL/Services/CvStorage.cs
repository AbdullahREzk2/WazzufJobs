namespace WazzufJobs.BLL.Services;

/// <summary>
/// Helpers for CV assets. The CVs table has no delivery-type column, so the type is derived
/// from the stored Cloudinary URL (".../raw/authenticated/..." vs legacy ".../raw/upload/...").
/// </summary>
public static class CvStorage
{
    public const string RawResourceType = "raw";
    public const string AuthenticatedType = "authenticated";
    public const string PublicUploadType = "upload";
    public const string CvFolder = "wazzuf-jobs/cvs";

    public static string DeliveryTypeFromUrl(string? storedUrl)
    {
        if (!string.IsNullOrEmpty(storedUrl) &&
            storedUrl.Contains("/raw/authenticated/", StringComparison.OrdinalIgnoreCase))
            return AuthenticatedType;

        return PublicUploadType;
    }

    /// <summary>Legacy CVs were uploaded as public raw assets before Phase C0. They are retained until approved remediation.</summary>
    public static bool IsLegacyPublic(string? storedUrl) =>
        DeliveryTypeFromUrl(storedUrl) == PublicUploadType;

    public static bool HasPdfSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5 &&
        bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D; // %PDF-
}
