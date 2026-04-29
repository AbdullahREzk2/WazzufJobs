namespace SurveyBasket.BLL.Features.Users.Query.GetProfile;
public class GetProfileQueryHandler(IUserRepository userRepository) : IRequestHandler<GetProfileQuery, Result<UserProfileResponse>>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result<UserProfileResponse>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure<UserProfileResponse>(UserErrors.UserNotFound);

        return Result.Success(user.Adapt<UserProfileResponse>());
    }
}
