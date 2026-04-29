using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;
public class UserPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public List<JobType> PreferredJobTypes { get; set; } = [];
    public List<WorkplaceType> PreferredWorkplaceTypes { get; set; } = [];
    public List<int> InterestedCategoryIds { get; set; } = [];
    public List<string> InterestedJobTitles { get; set; } = [];

    public decimal? MinSalary { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
}
