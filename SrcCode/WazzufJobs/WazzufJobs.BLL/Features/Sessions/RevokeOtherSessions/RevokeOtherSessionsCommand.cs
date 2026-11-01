using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Features.Sessions.RevokeOtherSessions;

/// <summary>"Sign out everywhere else": ends every session of the caller except the current one.</summary>
public record RevokeOtherSessionsCommand : IRequest<Result<int>>;

public class RevokeOtherSessionsCommandHandler(ICurrentUserService currentUser, ISessionService sessions, IAuditLog audit)
    : IRequestHandler<RevokeOtherSessionsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RevokeOtherSessionsCommand request, CancellationToken cancellationToken) =>
        await audit.RecordAsync(async () =>
                Result.Success((await sessions.RevokeAsync(currentUser.UserId!, null, currentUser.SessionId,
                    SessionPolicy.RevokedReason.SignedOutElsewhere, cancellationToken)).Count),
            r => AuditEvent.Success(AuditActions.OtherSessionsRevoked).On(AuditTargets.User, currentUser.UserId)
                .With(("revokedSessions", r.Value)),
            cancellationToken);
}
