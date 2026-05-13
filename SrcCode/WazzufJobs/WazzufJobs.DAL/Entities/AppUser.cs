using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;
public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string? ProfilePhotoUrl { get; set; }
    public string? ProfilePhotoPublicId { get; set; }

    // Onboarding
    public bool IsProfileComplete { get; set; } = false;
    public bool IsDisabled { get; set; } = false;
    public int ExperienceYears { get; set; }
    public CareerLevel CareerLevel { get; set; }
    public bool ShowSalary { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public CV? CV { get; set; }
    public UserPreference? Preference { get; set; }
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<SavedJob> SavedJobs { get; set; } = [];
    public ICollection<Job> PostedJobs { get; set; } = [];
}
