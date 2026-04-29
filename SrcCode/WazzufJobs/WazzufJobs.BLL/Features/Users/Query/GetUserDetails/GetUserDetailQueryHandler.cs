namespace SurveyBasket.BLL.Features.Users.Query.GetUserDetails;
public class GetUserDetailQueryHandler(IUserRepository userRepository) : IRequestHandler<GetUserDetailQuery, Result<UserResponse>>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result<UserResponse>> Handle(GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure<UserResponse>(UserErrors.UserNotFound);

        var userRoles = await _userrepository.GetRolesAsync(user);
        var response = (user, userRoles).Adapt<UserResponse>();

        return Result.Success(response);
    }
}
