namespace WazzufJobs.BLL.Features.Users.Command.CreateUser;
public class CreateUserCommandHandler(IUserRepository userRepository) : IRequestHandler<CreateUserCommand, Result<UserResponse>>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result<UserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {

        if (await _userrepository.EmailExistsAsync(request.userRequest.Email))
            return Result.Failure<UserResponse>(UserErrors.DuplicatedEmail);

        // Phase 02 (S3): only platform roles from the catalog; legacy aliases ("Admin", "User") are not roles
        var requestedRoles = request.userRequest.Roles;
        if (requestedRoles.Any(r => PermissionCatalog.Find(r) is null))
            return Result.Failure<UserResponse>(UserErrors.InvalidRoles);

        // staff accounts are never created through the Users API (D-STAFF-ROLES)
        if (requestedRoles.Any(PermissionCatalog.IsStaffRole))
            return Result.Failure<UserResponse>(UserErrors.StaffRolesManagedSeparately);

        // the request has no account type or demo flag: accounts created here are always real candidate accounts
        var user = request.userRequest.Adapt<AppUser>();
        user.UserName = request.userRequest.Email;
        user.EmailConfirmed = true;

        if (PermissionCatalog.ValidateAssignment(user.AccountType, user.IsDemo, requestedRoles) != RoleAssignmentError.None)
            return Result.Failure<UserResponse>(UserErrors.RoleAccountTypeMismatch);

        IList<string> roles = requestedRoles.Select(r => PermissionCatalog.Find(r)!.Name).Distinct().ToList();

        var result = await _userrepository.CreateAsync(user, request.userRequest.Password);
        if (!result.Succeeded)
        {
            var error = result.Errors.First();
            return Result.Failure<UserResponse>(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
        }

        var addRoleResult = await _userrepository.AddToRolesAsync(user, roles);
        if (!addRoleResult.Succeeded)
        {
            var error = addRoleResult.Errors.First();
            return Result.Failure<UserResponse>(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
        }

        var response = (user, roles).Adapt<UserResponse>();
        return Result.Success(response);
    }
}
