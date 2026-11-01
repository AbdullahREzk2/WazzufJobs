using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Audit;

/// <summary>
/// One audit entry to record. Details pass through the <see cref="AuditDetails"/> allowlist: only known keys with short,
/// plain values are kept (never passwords, tokens, emails, CV contents or storage keys).
/// </summary>
public sealed record AuditEvent(string Action, AuditOutcome Outcome)
{
    /// <summary>The acting account when it is not the signed-in caller (e.g. a successful sign-in).</summary>
    public string? ActorUserId { get; init; }

    /// <summary>Record no actor even if the caller is signed in (e.g. a failed sign-in for some account).</summary>
    public bool Anonymous { get; init; }

    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public Guid? CompanyId { get; init; }
    public bool IsDemo { get; init; }
    public IReadOnlyDictionary<string, object?>? Details { get; init; }

    /// <summary>
    /// Whether the request must fail when the entry cannot be written. Default: successful actions are required (no
    /// unaudited success); failures and denials are not, so an audit outage never changes a refusal or reveals, through
    /// a different response, which accounts exist.
    /// </summary>
    public bool Required { get; init; } = Outcome == AuditOutcome.Success;

    public static AuditEvent Success(string action) => new(action, AuditOutcome.Success);
    public static AuditEvent Failure(string action) => new(action, AuditOutcome.Failure);
    public static AuditEvent Denied(string action) => new(action, AuditOutcome.Denied);

    public AuditEvent On(string targetType, string? targetId) => this with { TargetType = targetType, TargetId = targetId };
    public AuditEvent With(params (string Key, object? Value)[] details) =>
        this with { Details = details.ToDictionary(d => d.Key, d => d.Value) };
}

public static class AuditTargets
{
    public const string User = "User";
    public const string Role = "Role";
    public const string Company = "Company";
    public const string CompanyMember = "CompanyMember";
    public const string Job = "Job";
    public const string Category = "Category";
    public const string Application = "Application";
    public const string Endpoint = "Endpoint";
    public const string Session = "Session";
}
