using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>Every status transition of an application, with actor and reason (Phase 02, §1).</summary>
public class ApplicationStatusHistory
{
    public long Id { get; set; }
    public int ApplicationId { get; set; }
    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }
    public string? ChangedById { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    // Navigation
    public Application Application { get; set; } = null!;
    public AppUser? ChangedBy { get; set; }
}
