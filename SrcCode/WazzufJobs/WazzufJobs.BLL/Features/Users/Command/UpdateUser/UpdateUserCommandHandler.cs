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

        var allowedRoles = await _mediator.Send(new GetAllRolesQuery());
        if (request.userRequest.Roles.Except(allowedRoles.Select(x => x.Name)).Any())
            return Result.Failure(UserErrors.InvalidRoles);

        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        user = request.userRequest.Adapt(user);
        user.UserName = request.userRequest.Email;
        user.NormalizedEmail = request.userRequest.Email.ToUpper();

        var result = await _userrepository.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _userrepository.DeleteUserRolesAsync(request.userId, cancellationToken);
            await _userrepository.AddToRolesAsync(user, request.userRequest.Roles);
            return Result.Success();
        }

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }
}
