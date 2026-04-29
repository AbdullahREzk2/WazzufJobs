using SurveyBasket.BLL.Features.Users.Command.UnlockUser;
using SurveyBasket.BLL.Features.Users.Query.GetUserDetails;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Features.Users.Command.CreateUser;
using WazzufJobs.BLL.Features.Users.Command.UpdateUser;
using WazzufJobs.BLL.Features.Users.Command.UserToggleStatus;
using WazzufJobs.BLL.Features.Users.Query.GetAllUsers;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.UsersRead)]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetAllUsersQuery(), cancellationToken));
    }

    [HttpGet("{userId}")]
    [HasPermission(Permissions.UsersRead)]
    public async Task<IActionResult> GetUserDetails(string userId)
    {
        var result = await _mediator.Send(new GetUserDetailQuery(userId));
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [HasPermission(Permissions.UsersCreate)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request,CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateUserCommand(request), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetUserDetails), new { userId = result.Value.Id }, result.Value)
            : result.ToProblem();
    }

    [HttpPut("{userId}")]
    [HasPermission(Permissions.UsersUpdate)]
    public async Task<IActionResult> UpdateUser(string userId,[FromBody] UpdateUserRequest request,CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateUserCommand(userId, request), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPut("{userId}/toggle-status")]
    [HasPermission(Permissions.UsersUpdate)]
    public async Task<IActionResult> ToggleStatus(string userId)
    {
        var result = await _mediator.Send(new ToggleStatusCommand(userId));
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPut("{userId}/unlock")]
    [HasPermission(Permissions.UsersUpdate)]
    public async Task<IActionResult> UnlockUser(string userId)
    {
        var result = await _mediator.Send(new UnlockUserCommand(userId));
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}