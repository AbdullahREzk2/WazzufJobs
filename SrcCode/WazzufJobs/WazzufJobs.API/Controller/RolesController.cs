using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Contracts.Roles;
using WazzufJobs.BLL.Features.Roles.Command.CreateRole;
using WazzufJobs.BLL.Features.Roles.Command.RoleToggleStatus;
using WazzufJobs.BLL.Features.Roles.Command.UpdateRole;
using WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;
using WazzufJobs.BLL.Features.Roles.Query.GetRoleDetails;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RolesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.RolesRead)]
    public async Task<IActionResult> GetAllRoles(CancellationToken cancellationToken)
    {
        var roles = await _mediator.Send(new GetAllRolesQuery(), cancellationToken);
        return Ok(roles);
    }

    [HttpGet("{id}")]
    [HasPermission(Permissions.RolesRead)]
    public async Task<IActionResult> GetRoleDetails(string id)
    {
        var result = await _mediator.Send(new GetRoleDetailsQuery(id));
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [HasPermission(Permissions.RolesCreate)]
    public async Task<IActionResult> CreateRole(
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateRoleCommand(request), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRoleDetails), new { id = result.Value.Id }, result.Value)
            : result.ToProblem();
    }

    [HttpPut("{roleId}")]
    [HasPermission(Permissions.RolesUpdate)]
    public async Task<IActionResult> UpdateRole(string roleId, [FromBody] RoleRequest request)
    {
        var result = await _mediator.Send(new UpdateRoleCommand(roleId, request));
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPut("{roleId}/toggle-status")]
    [HasPermission(Permissions.RolesUpdate)]
    public async Task<IActionResult> ToggleStatus(string roleId)
    {
        var result = await _mediator.Send(new RoleToggleStatusCommand(roleId));
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}