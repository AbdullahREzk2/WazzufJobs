using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Contracts.Jobs;
using WazzufJobs.BLL.Features.Jobs.Commands.CreateJob;
using WazzufJobs.BLL.Features.Jobs.Commands.DeleteJob;
using WazzufJobs.BLL.Features.Jobs.Commands.ToggleJobStatus;
using WazzufJobs.BLL.Features.Jobs.Commands.UpdateJob;
using WazzufJobs.BLL.Features.Jobs.Queries.GetAllJobs;
using WazzufJobs.BLL.Features.Jobs.Queries.GetJobById;
using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class JobsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [Authorize]   
    public async Task<IActionResult> GetAll([FromQuery] JobFilterRequestDTO filter,CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllJobsQuery(filter), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetJobByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [HasPermission(Permissions.JobsCreate)]  
    public async Task<IActionResult> Create([FromBody] JobRequest request,CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _mediator.Send(new CreateJobCommand(request, userId!), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblem();
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.JobsUpdate)]  
    public async Task<IActionResult> Update(int id,[FromBody] JobRequest request,CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateJobCommand(id, request), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpDelete("{id}")]
    [HasPermission(Permissions.JobsDelete)]  
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteJobCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPut("{id}/toggle-status")]
    [HasPermission(Permissions.JobsUpdate)]  
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ToggleJobStatusCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}