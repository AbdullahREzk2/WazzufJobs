namespace WazzufJobs.DAL.Entities;

/// <summary>Single-use two-factor recovery code (Phase 02, §6). Only the hash is stored.</summary>
public class UserRecoveryCode
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public byte[] CodeHash { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
}
