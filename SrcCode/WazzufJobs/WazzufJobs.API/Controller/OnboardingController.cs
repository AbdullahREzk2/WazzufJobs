using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WazzufJobs.API.Extensions;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Onboarding;
using WazzufJobs.BLL.Features.Onboarding.Commands.CompleteOnboarding;
using WazzufJobs.BLL.Features.Onboarding.Queries.GetOnboardingStatus;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OnboardingController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new GetOnboardingStatusQuery(userId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(
        [FromBody] OnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()!;

        var result = await _mediator.Send(
            new CompleteOnboardingCommand(userId, request), cancellationToken);

        return result.IsSuccess ? Ok() : result.ToProblem();
    }
}