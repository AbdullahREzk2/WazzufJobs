namespace WazzufJobs.DAL.Entities;

/// <summary>
/// A signed-in session (Phase 02, §5). Its id is the access token's <c>sid</c> claim. Sessions are revoked, never deleted
/// by the application, so revocation stays auditable.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime AbsoluteExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    /// <summary>Authentication method reference: "pwd" or "mfa".</summary>
    public string Amr { get; set; } = "pwd";
    public string? UserAgent { get; set; }

    /// <summary>HMAC of the client address (never the address itself).</summary>
    public byte[]? IpHash { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public AppUser User { get; set; } = null!;
    public ICollection<SessionRefreshToken> RefreshTokens { get; set; } = [];
}
