namespace WazzufJobs.BLL.Contracts.Sessions;

/// <summary>One of the caller's sessions. No address (not even its hash) and no token material.</summary>
public record SessionResponse(Guid Id, DateTime CreatedAt, DateTime LastSeenAt, DateTime ExpiresAt, string Method, string? UserAgent, bool Current);
