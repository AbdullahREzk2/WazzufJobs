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

    // Navigation
    public AppUser User { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
