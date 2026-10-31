using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>
/// A candidate's resume file in private storage (Phase 02, §1; replaces <c>CVs</c> in S2b). The storage key is never
/// returned by the API. A resume referenced by an application cannot be deleted (archive instead).
/// </summary>
public class Resume
{
    public int Id { get; set; }
    public string CandidateId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public ResumeStorageAccess StorageAccess { get; set; }
    public long SizeBytes { get; set; }
    public byte[] Sha256 { get; set; } = [];
    public bool IsDefault { get; set; }
    public ResumeStatus Status { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public AppUser Candidate { get; set; } = null!;
    public ResumeText? Text { get; set; }
}
