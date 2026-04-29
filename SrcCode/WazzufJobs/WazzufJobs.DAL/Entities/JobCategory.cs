namespace WazzufJobs.DAL.Entities;
public class JobCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;        // "Software & IT"
    public string Slug { get; set; } = string.Empty; // e.g. "software-it"

    public string? IconUrl { get; set; }
    public string? IconPublicId { get; set; }
    // Navigation
    public ICollection<Job> Jobs { get; set; } = [];
}
