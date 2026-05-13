using WazzufJobs.BLL.Features.Roles.Query.GetAllRoles;

namespace WazzufJobs.BLL.Features.Users.Command.CreateUser;
public class CreateUserCommandHandler(IUserRepository userRepository, IMediator mediator) : IRequestHandler<CreateUserCommand, Result<UserResponse>>
{
    private readonly IUserRepository _userrepository = userRepository;
    private readonly IMediator _mediator = mediator;

    public async Task<Result<UserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {

        if (await _userrepository.EmailExistsAsync(request.userRequest.Email))
            return Result.Failure<UserResponse>(UserErrors.DuplicatedEmail);

        var allowedRoles = await _mediator.Send(new GetAllRolesQuery());
        var allowedRoleNames = allowedRoles.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.userRequest.Roles.Any(r => !allowedRoleNames.Contains(r)))
            return Result.Failure<UserResponse>(UserErrors.InvalidRoles);

        var user = request.userRequest.Adapt<AppUser>();
        user.UserName = request.userRequest.Email;
        user.EmailConfirmed = true;

        var result = await _userrepository.CreateAsync(user, request.userRequest.Password);
        if (!result.Succeeded)
        {
            var error = result.Errors.First();
            return Result.Failure<UserResponse>(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
        }

        var addRoleResult = await _userrepository.AddToRolesAsync(user, request.userRequest.Roles);
        if (!addRoleResult.Succeeded)
        {
            var error = addRoleResult.Errors.First();
            return Result.Failure<UserResponse>(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
        }

        var response = (user, request.userRequest.Roles).Adapt<UserResponse>();
        return Result.Success(response);
    }
}
