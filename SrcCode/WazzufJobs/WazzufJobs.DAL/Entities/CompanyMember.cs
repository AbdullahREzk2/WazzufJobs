using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>
/// Membership of a user in a company (Phase 02, §3). A user has at most one active membership (filtered unique index).
/// </summary>
public class CompanyMember
{
    public Guid CompanyId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public CompanyMemberRole Role { get; set; }
    public CompanyMemberStatus Status { get; set; }
    public string? AddedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Company Company { get; set; } = null!;
    public AppUser User { get; set; } = null!;
    public AppUser? AddedBy { get; set; }
}
