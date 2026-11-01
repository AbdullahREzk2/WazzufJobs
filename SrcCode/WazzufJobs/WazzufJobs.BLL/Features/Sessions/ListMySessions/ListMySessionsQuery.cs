using WazzufJobs.BLL.Contracts.Sessions;
using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Features.Sessions.ListMySessions;

public record ListMySessionsQuery : IRequest<IReadOnlyList<SessionResponse>>;

/// <summary>The caller's active sessions (Phase 02, S6a). Only ever the caller's own, identified by the access token.</summary>
public class ListMySessionsQueryHandler(ICurrentUserService currentUser, ISessionService sessions)
    : IRequestHandler<ListMySessionsQuery, IReadOnlyList<SessionResponse>>
{
    public async Task<IReadOnlyList<SessionResponse>> Handle(ListMySessionsQuery request, CancellationToken cancellationToken)
    {
        var current = currentUser.SessionId;
        return (await sessions.ListActiveAsync(currentUser.UserId!, cancellationToken))
            .Select(s => new SessionResponse(s.Id, s.CreatedAt, s.LastSeenAt, s.AbsoluteExpiresAt, s.Amr, s.UserAgent, s.Id == current))
            .ToList();
    }
}
