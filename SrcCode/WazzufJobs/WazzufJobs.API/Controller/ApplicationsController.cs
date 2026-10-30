using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Features.Applications.Commands.ApplyForJob;
using WazzufJobs.BLL.Features.Applications.Commands.UpdateApplicationStatus;
using WazzufJobs.BLL.Features.Applications.Queries.GetApplicationDetail;
using WazzufJobs.BLL.Features.Applications.Queries.GetApplicationsByJob;
using WazzufJobs.BLL.Features.Applications.Queries.GetMyApplications;
using WazzufJobs.BLL.Features.UserCV.Queries.GetCvFile;
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
    // Phase C0 (S1): `applications:read` is also granted to job seekers (for their own applications),
    // so it must not protect cross-user data. These endpoints require `applications:update`, which is
    // seeded for the Admin role only. Replaced by a dedicated permission in Phase 03.
    private const string AdminApplicationsPermission = Permissions.ApplicationsUpdate;

    [HttpGet("job/{jobId}")]
    [HasPermission(AdminApplicationsPermission)]
    public async Task<IActionResult> GetByJob(int jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetApplicationsByJobQuery(jobId, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission(AdminApplicationsPermission)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetApplicationDetailQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}/status")]
    [HasPermission(AdminApplicationsPermission)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateApplicationStatusCommand(id, request), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpGet("{id}/cv")]
    [HasPermission(AdminApplicationsPermission)]
    public async Task<IActionResult> GetApplicationCv(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetApplicationCvFileQuery(id), cancellationToken);
        return this.ToCvFileResult(result);
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
