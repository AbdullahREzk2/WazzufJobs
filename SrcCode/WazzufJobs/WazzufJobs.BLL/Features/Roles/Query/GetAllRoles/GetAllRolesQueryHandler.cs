namespace WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;
public class GetAllRolesQueryHandler(IRoleRepository roleRepository) : IRequestHandler<GetAllRolesQuery, IEnumerable<RoleResponse>>
{
    private readonly IRoleRepository _rolerepository = roleRepository;

    public async Task<IEnumerable<RoleResponse>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _rolerepository.getAllRoles(request.includeDisabled, cancellationToken);
        return roles.Adapt<IEnumerable<RoleResponse>>();
    }
}
