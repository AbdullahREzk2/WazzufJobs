using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>
/// Candidate profile and job preferences (Phase 02, §1). Replaces the onboarding columns on <c>AspNetUsers</c> and the
/// <c>UserPreferences</c> table once handlers are ported (S2b).
/// </summary>
public class CandidateProfile
{
    public string UserId { get; set; } = string.Empty;
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public int ExperienceYears { get; set; }
    public CareerLevel CareerLevel { get; set; }
    public bool ShowSalary { get; set; }
    public bool IsProfileComplete { get; set; }
    public List<JobType> PreferredJobTypes { get; set; } = [];
    public List<WorkplaceType> PreferredWorkplaceTypes { get; set; } = [];
    public List<int> InterestedCategoryIds { get; set; } = [];
    public List<string> InterestedJobTitles { get; set; } = [];
    public decimal? MinSalary { get; set; }
    public bool IsDemo { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public AppUser User { get; set; } = null!;
}
