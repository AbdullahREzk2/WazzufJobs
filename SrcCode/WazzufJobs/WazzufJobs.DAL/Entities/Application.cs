using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;
public class Application
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int JobId { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    // AI Matching result 
    public float? AIScore { get; set; }
    public string? AIFeedback { get; set; }
    public bool IsAIScored { get; set; } = false;

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    // Phase 02: company copied from the job and the applicant's resume (both required). Composite foreign keys
    // (JobId, CompanyId) and (ResumeId, CandidateId) make a mismatched company or another candidate's resume impossible.
    public Guid CompanyId { get; set; }
    public int ResumeId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDemo { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public AppUser User { get; set; } = null!;
    public Job Job { get; set; } = null!;
    public Company Company { get; set; } = null!;
    public Resume Resume { get; set; } = null!;
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = [];
}
