namespace WazzufJobs.DAL.Entities;
public class SavedJob
{
    public string UserId { get; set; } = string.Empty;
    public int JobId { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
