using WazzufJobs.BLL.Audit;
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
public class RolesController(IMediator mediator, IAuditLog audit) : ControllerBase
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
    public Task<IActionResult> CreateRole() => RolesAreFixedAsync(null, "create");

    [HttpPut("{roleId}")]
    [HasPermission(Permissions.RolesRead)]
    public Task<IActionResult> UpdateRole(string roleId) => RolesAreFixedAsync(roleId, "update");

    [HttpPut("{roleId}/toggle-status")]
    [HasPermission(Permissions.RolesRead)]
    public Task<IActionResult> ToggleStatus(string roleId) => RolesAreFixedAsync(roleId, "toggle-status");

    private async Task<IActionResult> RolesAreFixedAsync(string? roleId, string operation)
    {
        await audit.WriteAsync(AuditEvent.Denied(AuditActions.RolesChangeDenied)
            .On(AuditTargets.Role, PermissionCatalog.FindById(roleId)?.Id)
            .With(("reason", RoleErros.Fixed.Code), ("method", operation)));
        return Result.Failure(RoleErros.Fixed).ToProblem();
    }
}
