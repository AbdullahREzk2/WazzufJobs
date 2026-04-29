using WazzufJobs.DAL.Enums;
using static System.Net.Mime.MediaTypeNames;

namespace WazzufJobs.DAL.Entities;
public class Job
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public List<string> Skills { get; set; } = [];

    public JobType JobType { get; set; }
    public WorkplaceType WorkplaceType { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Active;

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }

    public int CategoryId { get; set; }
    public string PostedById { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public JobCategory Category { get; set; } = null!;
    public AppUser PostedBy { get; set; } = null!;
    public ICollection<Application> Applications { get; set; } = [];
}
