using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Services;

/// <summary>
/// Helpers for resume (CV) assets. The storage delivery type is stored with the resume
/// (<see cref="ResumeStorageAccess"/>); no storage URL is kept in the database (Phase 02 S2b).
/// </summary>
public static class CvStorage
{
    public const string RawResourceType = "raw";
    public const string AuthenticatedType = "authenticated";
    public const string PublicUploadType = "upload";
    public const string CvFolder = "wazzuf-jobs/cvs";

    public static string DeliveryType(ResumeStorageAccess access) =>
        access == ResumeStorageAccess.Authenticated ? AuthenticatedType : PublicUploadType;

    /// <summary>Legacy CVs were uploaded as public raw assets before Phase C0. They are retained until approved remediation.</summary>
    public static bool IsLegacyPublic(ResumeStorageAccess access) =>
        access == ResumeStorageAccess.LegacyPublic;

    public static bool HasPdfSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5 &&
        bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D; // %PDF-
}
