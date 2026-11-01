namespace WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;

/// <summary>The fixed platform roles, read from the code catalog (Phase 02, S3).</summary>
public class GetAllRolesQueryHandler : IRequestHandler<GetAllRolesQuery, IEnumerable<RoleResponse>>
{
    public Task<IEnumerable<RoleResponse>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RoleResponse>>(PermissionCatalog.Roles
            .Select(r => new RoleResponse(r.Id, r.Name, false, r.Description, r.AccountType.ToString()))
            .ToList());
}
