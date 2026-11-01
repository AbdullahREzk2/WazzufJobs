namespace WazzufJobs.BLL.Features.Users.Command.UserToggleStatus;
public class ToggleStatusCommandHandler(IUserRepository userRepository) : IRequestHandler<ToggleStatusCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(ToggleStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        var superAdmin = PermissionCatalog.SuperAdmin;
        var disablesSuperAdmin = !user.IsDisabled
            && superAdmin.IsCompatibleWith(user.AccountType, user.IsDemo)
            && (await _userrepository.GetRolesAsync(user)).Contains(superAdmin.Name, StringComparer.OrdinalIgnoreCase);

        if (!disablesSuperAdmin)
            return await ToggleAsync(user);

        // never leave the platform without an active Super Admin (C0-5, generalized in S3). The count and the change run
        // under the SuperAdmin role lock, so two Super Admins disabling each other cannot both succeed.
        return await _userrepository.RunExclusiveForRoleAsync(superAdmin, async () =>
            await _userrepository.CountActiveUsersInRoleAsync(superAdmin, cancellationToken) <= 1
                ? Result.Failure(UserErrors.LastAdministrator)
                : await ToggleAsync(user),
            result => result.IsSuccess,
            cancellationToken);
    }

    private async Task<Result> ToggleAsync(AppUser user)
    {
        user.IsDisabled = !user.IsDisabled;
        var result = await _userrepository.UpdateAsync(user);

        if (result.Succeeded)
            return Result.Success();

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }
}
