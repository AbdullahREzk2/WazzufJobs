using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;
public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string? ProfilePhotoUrl { get; set; }
    public string? ProfilePhotoPublicId { get; set; }

    public bool IsDisabled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Phase 02 (S2a)
    public AccountType AccountType { get; set; } = AccountType.Candidate;
    public bool IsDemo { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Last accepted TOTP time step (replay protection).</summary>
    public long? TotpLastTimeStep { get; set; }

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<SavedJob> SavedJobs { get; set; } = [];
    public ICollection<Job> PostedJobs { get; set; } = [];
    // onboarding and job preferences (Phase 02 S2b: replaces the onboarding columns and UserPreferences)
    public CandidateProfile? CandidateProfile { get; set; }
    public ICollection<Resume> Resumes { get; set; } = [];
    public ICollection<UserSession> Sessions { get; set; } = [];
    public ICollection<CompanyMember> CompanyMemberships { get; set; } = [];
}
