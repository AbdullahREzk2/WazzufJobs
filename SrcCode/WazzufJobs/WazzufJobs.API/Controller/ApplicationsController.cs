using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Features.Applications.Commands.ApplyForJob;
using WazzufJobs.BLL.Features.Applications.Commands.UpdateApplicationStatus;
using WazzufJobs.BLL.Features.Applications.Queries.GetApplicationDetail;
using WazzufJobs.BLL.Features.Applications.Queries.GetApplicationsByJob;
using WazzufJobs.BLL.Features.Applications.Queries.GetMyApplications;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Settings;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controller;
[Route("api/[controller]")]
[ApiController]

public class ApplicationsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    // ── Admin endpoints ──────────────────────────────────

    [HttpGet("job/{jobId}")]
    [HasPermission(Permissions.ApplicationsRead)]
    public async Task<IActionResult> GetByJob(int jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetApplicationsByJobQuery(jobId, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission(Permissions.ApplicationsRead)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetApplicationDetailQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}/status")]
    [HasPermission(Permissions.ApplicationsUpdate)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateApplicationStatusCommand(id, request), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    // ── User endpoints ───────────────────────────────────

    [HttpPost("job/{jobId}/apply")]
    [HasPermission(Permissions.ApplicationsCreate)]
    public async Task<IActionResult> Apply(int jobId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new ApplyForJobCommand(jobId, userId), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDetail), new { id = result.Value }, null)
            : result.ToProblem();
    }

    [HttpGet("my-applications")]
    [HasPermission(Permissions.ApplicationsRead)]
    public async Task<IActionResult> GetMyApplications([FromQuery] int page = 1,[FromQuery] int pageSize = 10,CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new GetMyApplicationsQuery(userId, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

}
