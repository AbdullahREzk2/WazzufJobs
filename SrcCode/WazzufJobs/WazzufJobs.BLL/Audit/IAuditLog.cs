using Microsoft.Extensions.Logging;
using WazzufJobs.DAL.Enums;
using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Audit;

/// <summary>
/// The central audit log (Phase 02, S5; architecture §7). Entries are append-only (database trigger), carry actor,
/// action, target, time, outcome, correlation id, a keyed hash of the client address and allowlisted details.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Runs a privileged change and its audit entry atomically. A successful result is committed together with its
    /// success entry in one transaction; if the entry cannot be written the change is rolled back and the error
    /// propagates (no unaudited change). A failed result is rolled back and its entry, if any, is written separately.
    /// </summary>
    Task<TResult> RecordAsync<TResult>(Func<Task<TResult>> change, Func<TResult, AuditEvent?> describe, CancellationToken cancellationToken)
        where TResult : Result;

    /// <summary>
    /// Writes an entry on its own (sign-in events, failures, denials, reads such as CV access). A
    /// <see cref="AuditEvent.Required"/> entry that cannot be written throws; any other failure is logged as an error and
    /// the request continues unchanged.
    /// </summary>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public sealed class AuditLogService(
    ICurrentUserService currentUser,
    IRequestInfo request,
    IAuditRepository repository,
    IIpHasher ipHasher,
    ILogger<AuditLogService> logger) : IAuditLog
{
    public async Task<TResult> RecordAsync<TResult>(
        Func<Task<TResult>> change, Func<TResult, AuditEvent?> describe, CancellationToken cancellationToken)
        where TResult : Result
    {
        AuditEvent? unsuccessful = null;
        var result = await repository.RunInTransactionAsync(async () =>
        {
            unsuccessful = null;
            var outcome = await change();
            var auditEvent = describe(outcome);
            if (outcome.IsSuccess && auditEvent is not null)
                await repository.AppendAsync(Build(auditEvent with { Outcome = AuditOutcome.Success }), cancellationToken);
            else if (!outcome.IsSuccess && auditEvent?.Outcome != AuditOutcome.Success)
                unsuccessful = auditEvent;   // a failed change is never recorded as a success
            return outcome;
        }, outcome => outcome.IsSuccess, cancellationToken);

        if (!result.IsSuccess && unsuccessful is not null)
            await WriteAsync(unsuccessful, CancellationToken.None);
        return result;
    }

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.AppendSeparatelyAsync(Build(auditEvent), cancellationToken);
        }
        catch (Exception ex) when (!auditEvent.Required)
        {
            // never silent, never a different response: the refusal or failure stands as it is
            logger.LogError(ex, "Audit entry {Action} ({Outcome}) could not be written.", auditEvent.Action, auditEvent.Outcome);
        }
    }

    private AuditLog Build(AuditEvent e)
    {
        var actor = e.ActorUserId ?? (e.Anonymous ? null : currentUser.UserId);
        return new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            ActorUserId = actor,
            ActorKind = actor is not null ? AuditActorKind.User : request.HasRequest ? AuditActorKind.Anonymous : AuditActorKind.System,
            Action = e.Action,
            TargetType = e.TargetType,
            TargetId = Truncate(e.TargetId, 100),
            CompanyId = e.CompanyId,
            IsDemo = e.IsDemo,
            Outcome = e.Outcome,
            IpHash = ipHasher.Hash(request.RemoteIpAddress),
            UserAgent = Truncate(Printable(request.UserAgent), 200),
            CorrelationId = Truncate(request.CorrelationId, 100),
            Details = AuditDetails.Serialize(e.Details)
        };
    }

    private static string? Printable(string? value) =>
        value is null ? null : new string(value.Where(c => !char.IsControl(c)).ToArray());

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
