using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Entities;

/// <summary>
/// Append-only audit record (Phase 02, §7). A database trigger rejects UPDATE and DELETE. Actor and company are plain
/// columns (no foreign keys) so records survive account and company changes. Details never contain secrets or tokens.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? ActorUserId { get; set; }
    public AuditActorKind ActorKind { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsDemo { get; set; }
    public AuditOutcome Outcome { get; set; }
    public byte[]? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
}
