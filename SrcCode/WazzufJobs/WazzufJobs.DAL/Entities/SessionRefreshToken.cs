namespace WazzufJobs.DAL.Entities;

/// <summary>
/// Refresh token of a <see cref="UserSession"/> (Phase 02, §5.2), table <c>RefreshTokens</c>. Only the SHA-256 hash is
/// stored. A filtered unique index guarantees at most one usable (not consumed, not revoked) token per session.
/// </summary>
public class SessionRefreshToken
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }

    /// <summary>The token this one replaced on rotation (null for a session's first token).</summary>
    public long? ParentId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public UserSession Session { get; set; } = null!;
    public SessionRefreshToken? Parent { get; set; }
}
