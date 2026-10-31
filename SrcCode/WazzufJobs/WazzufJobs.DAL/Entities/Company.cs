using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>An employer (Phase 02, §1 and §3). Every company-scoped query is filtered by the company id.</summary>
public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? Industry { get; set; }
    public string? Size { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? LogoKey { get; set; }
    public CompanyVerificationStatus VerificationStatus { get; set; }
    public CompanyStatus Status { get; set; }

    /// <summary>The single platform company that owns jobs created by platform staff (until P04).</summary>
    public bool IsPlatform { get; set; }
    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public AppUser? CreatedBy { get; set; }
    public ICollection<CompanyMember> Members { get; set; } = [];
    public ICollection<Job> Jobs { get; set; } = [];
}
