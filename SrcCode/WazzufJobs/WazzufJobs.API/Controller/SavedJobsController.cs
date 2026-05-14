using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Features.SavedJobs.Commands.RemoveSavedJob;
using WazzufJobs.BLL.Features.SavedJobs.Commands.SaveJob;
using WazzufJobs.BLL.Features.SavedJobs.Queries.GetSavedJobs;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SavedJobsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.SavedJobsRead)]
    public async Task<IActionResult> GetSavedJobs(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new GetSavedJobsQuery(userId), cancellationToken);

        return Ok(result);
    }

    [HttpPost("{jobId}")]
    [HasPermission(Permissions.SavedJobsCreate)]
    public async Task<IActionResult> SaveJob(int jobId,CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new SaveJobCommand(userId, jobId), cancellationToken);

        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    [HttpDelete("{jobId}")]
    [HasPermission(Permissions.SavedJobsDelete)]
    public async Task<IActionResult> RemoveSavedJob(int jobId,CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new RemoveSavedJobCommand(userId, jobId), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}