using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>
/// Short-lived, single-use authentication challenge (Phase 02, §5.3 and §6): a TOTP challenge, a TOTP enrollment, or a
/// browser-bound session hand-off. Only hashes are stored.
/// </summary>
public class AuthChallenge
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AuthChallengePurpose Purpose { get; set; }
    public byte[] TokenHash { get; set; } = [];

    /// <summary>Session a hand-off belongs to (hand-off challenges only).</summary>
    public Guid? SessionId { get; set; }

    /// <summary>SHA-256 of the browser binding value (hand-off challenges only).</summary>
    public byte[]? BindingHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public int Attempts { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
    public UserSession? Session { get; set; }
}
