using Microsoft.EntityFrameworkCore;
using WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;

namespace WazzufJobs.BLL.Features.Roles.Command.UpdateRole;

public class UpdateRoleCommandHandler(IRoleRepository roleRepository, RoleManager<ApplicationRole> roleManager)
    : IRequestHandler<UpdateRoleCommand, Result>
{
    private readonly IRoleRepository _rolerepository = roleRepository;
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var strategy = _rolerepository.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _rolerepository.BeginTransactionAsync();

            try
            {
                // 1. Validation
                var isRoleExist = await _rolerepository.isRoleNameExist(request.RoleId, request.roleRequest.Name);
                if (isRoleExist)
                    return Result.Failure(RoleErros.RoleAlreadyExist);

                var role = await _rolerepository.getRoleById(request.RoleId);
                if (role is null)
                    return Result.Failure(RoleErros.RoleNotFound);

                if (request.roleRequest.Permissions is null)
                    return Result.Failure(RoleErros.InvalidPermissions);

                var allowedPermissions = Permissions.GetAllPermissions();

                if (request.roleRequest.Permissions.Except(allowedPermissions).Any())
                    return Result.Failure(RoleErros.InvalidPermissions);

                // 2. FIX Identity issue
                role.Name = request.roleRequest.Name;
                role.NormalizedName = role.Name.ToUpper();

                var updateResult = await _rolerepository.UpdateRole(role);

                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var error = updateResult.Errors.First();
                    return Result.Failure(new Error(error.Code, error.Description, 400));
                }

                // 3. Permissions
                var currentPermissions =
                    (await _rolerepository.getRolePermissions(role)) ?? new List<string>();

                var newPermissions =
                    request.roleRequest.Permissions.Except(currentPermissions);

                var removedPermissions =
                    currentPermissions.Except(request.roleRequest.Permissions);

                var removedResult = await _rolerepository
                    .removePermissionForRole(request.RoleId, removedPermissions);

                if (!removedResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var error = removedResult.Errors.First();
                    return Result.Failure(new Error(error.Code, error.Description, 400));
                }

                var addResult = await _rolerepository
                    .setPermissionsForRole(role, newPermissions);

                if (!addResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var error = addResult.Errors.First();
                    return Result.Failure(new Error(error.Code, error.Description, 400));
                }

                await transaction.CommitAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result.Failure(new Error("Exception", ex.Message, 500));
            }
        });
    }
}