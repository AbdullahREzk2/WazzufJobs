using WazzufJobs.BLL.Features.Sessions.ListMySessions;
using WazzufJobs.BLL.Features.Sessions.RevokeMySession;
using WazzufJobs.BLL.Features.Sessions.RevokeOtherSessions;

namespace WazzufJobs.API.Controller;

/// <summary>
/// The caller's own sessions (Phase 02, S6a; architecture §5.2). Any signed-in account; a session of another account
/// is "not found". Transport-neutral sign-out is <c>DELETE current</c>; the cookie endpoints arrive in S6b.
/// </summary>
[Route("api/v1/me/sessions")]
[ApiController]
[Authorize]
public class MySessionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListMySessionsQuery(), cancellationToken));

    [HttpDelete("current")]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RevokeMySessionCommand(null), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> Revoke([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RevokeMySessionCommand(sessionId), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPost("revoke-others")]
    public async Task<IActionResult> RevokeOthers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RevokeOtherSessionsCommand(), cancellationToken);
        return result.IsSuccess ? Ok(new { revoked = result.Value }) : result.ToProblem();
    }
}
