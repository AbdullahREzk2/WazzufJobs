using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Features.Sessions.RevokeMySession;

/// <summary>Ends one of the caller's sessions; null means the current one (sign-out, transport-neutral).</summary>
public record RevokeMySessionCommand(Guid? SessionId) : IRequest<Result>;

public class RevokeMySessionCommandHandler(ICurrentUserService currentUser, ISessionService sessions, IAuditLog audit)
    : IRequestHandler<RevokeMySessionCommand, Result>
{
    public async Task<Result> Handle(RevokeMySessionCommand request, CancellationToken cancellationToken)
    {
        var target = request.SessionId ?? currentUser.SessionId;
        if (target is null)
            return Result.Failure(SessionErrors.NotFound);

        var reason = target == currentUser.SessionId ? SessionPolicy.RevokedReason.Logout : SessionPolicy.RevokedReason.SignedOutElsewhere;
        // revocation and its audit entry are committed together; another user's session is simply "not found"
        return await audit.RecordAsync(async () =>
                (await sessions.RevokeAsync(currentUser.UserId!, target, null, reason, cancellationToken)).Count == 1
                    ? Result.Success()
                    : Result.Failure(SessionErrors.NotFound),
            r => r.IsSuccess
                ? AuditEvent.Success(AuditActions.SessionRevoked).On(AuditTargets.Session, target.ToString()).With(("reason", reason))
                : null,
            cancellationToken);
    }
}
