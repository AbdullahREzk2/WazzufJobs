namespace WazzufJobs.BLL.Features.Users.Command.UserToggleStatus;
public class ToggleStatusCommandHandler(IUserRepository userRepository) : IRequestHandler<ToggleStatusCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(ToggleStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        var disabling = !user.IsDisabled;
        if (disabling
            && (await _userrepository.GetRolesAsync(user)).Contains(AppRoles.Admin.Name, StringComparer.OrdinalIgnoreCase)
            && await _userrepository.CountActiveUsersInRoleAsync(AppRoles.Admin.Name, cancellationToken) <= 1)
            return Result.Failure(UserErrors.LastAdministrator);

        user.IsDisabled = !user.IsDisabled;
        var result = await _userrepository.UpdateAsync(user);

        if (result.Succeeded)
            return Result.Success();

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }
}
