using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Errors;
using WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;
using WazzufJobs.BLL.Features.Roles.Query.GetRoleDetails;
using WazzufJobs.DAL.Authorization;

namespace WazzufJobs.API.Controllers;

/// <summary>
/// Read-only view of the fixed platform roles (Phase 02, S3). Roles and their permissions are defined in code
/// (<c>PermissionCatalog</c>); the former mutation routes stay mapped and always answer 403 <c>Roles.Fixed</c>.
/// </summary>
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

    // The request body is deliberately not bound: every mutation is refused, whatever it contains.

    [HttpPost]
    [HasPermission(Permissions.RolesRead)]
    public IActionResult CreateRole() => RolesAreFixed();

    [HttpPut("{roleId}")]
    [HasPermission(Permissions.RolesRead)]
    public IActionResult UpdateRole(string roleId) => RolesAreFixed();

    [HttpPut("{roleId}/toggle-status")]
    [HasPermission(Permissions.RolesRead)]
    public IActionResult ToggleStatus(string roleId) => RolesAreFixed();

    private IActionResult RolesAreFixed() => Result.Failure(RoleErros.Fixed).ToProblem();
}
