namespace WazzufJobs.BLL.Features.Roles.Query.GetRoleDetails;
public class GetRoleDetailsQueryHandler(IRoleRepository roleRepository) : IRequestHandler<GetRoleDetailsQuery, Result<RoleDetailResponse>>
{
    private readonly IRoleRepository _rolerepository = roleRepository;

    public async Task<Result<RoleDetailResponse>> Handle(GetRoleDetailsQuery request, CancellationToken cancellationToken)
    {

        var role = await _rolerepository.getRoleById(request.RoleId);

        if (role is not { })
            return Result.Failure<RoleDetailResponse>(RoleErros.RoleNotFound);

        var permissions = await _rolerepository.getRolePermissions(role);

        var response = new RoleDetailResponse(role.Id, role.Name!, role.IsDeleted, permissions);

        return Result.Success(response);
    }
}
