namespace WazzufJobs.BLL.Features.Roles.Command.CreateRole;
public class CreateRoleCommandHandler(IRoleRepository roleRepository) : IRequestHandler<CreateRoleCommand, Result<RoleDetailResponse>>
{
    private readonly IRoleRepository _rolerepository = roleRepository;

    public async Task<Result<RoleDetailResponse>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {

        var isRoleExist = await _rolerepository.isRoleExist(request.roleRequest.Name);

        if (isRoleExist)
            return Result.Failure<RoleDetailResponse>(RoleErros.RoleAlreadyExist);

        var allowedPermissions = Permissions.GetAllPermissions();

        if (request.roleRequest.Permissions.Except(allowedPermissions).Any())
            return Result.Failure<RoleDetailResponse>(RoleErros.InvalidPermissions);

        var newRole = new ApplicationRole
        {
            Name = request.roleRequest.Name,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        var createResult = await _rolerepository.CreateRole(newRole);

        if (!createResult.Succeeded)
            return Result.Failure<RoleDetailResponse>(RoleErros.CreationFailed);

        var permissionsResult = await _rolerepository.setPermissionsForRole(newRole, request.roleRequest.Permissions);

        if (!permissionsResult.Succeeded)
            return Result.Failure<RoleDetailResponse>(RoleErros.PermissionAssignmentFailed);

        var response = new RoleDetailResponse(newRole.Id, newRole.Name, newRole.IsDeleted, request.roleRequest.Permissions);
        return Result.Success(response);
    }
}
