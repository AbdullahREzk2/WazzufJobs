using WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;

namespace WazzufJobs.BLL.Features.Users.Command.UpdateUser;
public class UpdateUserCommandHandler(IUserRepository userRepository, IMediator mediator) : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly IMediator _mediator = mediator;

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {

        if (await _userrepository.EmailExistsForOtherUserAsync(request.userRequest.Email, request.userId, cancellationToken))
            return Result.Failure(UserErrors.DuplicatedEmail);

        var requestedRoles = request.userRequest.Roles ?? [];

        var allowedRoles = await _mediator.Send(new GetAllRolesQuery());
        if (requestedRoles.Except(allowedRoles.Select(x => x.Name)).Any())
            return Result.Failure(UserErrors.InvalidRoles);

        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        // never leave the platform without an administrator
        var currentRoles = await _userrepository.GetRolesAsync(user);
        var removesAdmin = currentRoles.Contains(AppRoles.Admin.Name, StringComparer.OrdinalIgnoreCase)
            && !requestedRoles.Contains(AppRoles.Admin.Name, StringComparer.OrdinalIgnoreCase);
        if (removesAdmin && await _userrepository.CountActiveUsersInRoleAsync(AppRoles.Admin.Name, cancellationToken) <= 1)
            return Result.Failure(UserErrors.LastAdministrator);

        user = request.userRequest.Adapt(user);
        user.UserName = request.userRequest.Email;
        user.NormalizedEmail = request.userRequest.Email.ToUpper();

        var result = await _userrepository.UpdateAsync(user);
        if (result.Succeeded)
        {
            var rolesResult = await _userrepository.ReplaceUserRolesAsync(user, requestedRoles, cancellationToken);
            if (!rolesResult.Succeeded)
            {
                var rolesError = rolesResult.Errors.First();
                return Result.Failure(new Error(rolesError.Code, rolesError.Description, StatusCodes.Status400BadRequest));
            }

            return Result.Success();
        }

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }
}
