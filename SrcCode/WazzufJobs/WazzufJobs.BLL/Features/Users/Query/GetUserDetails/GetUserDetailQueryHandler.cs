namespace SurveyBasket.BLL.Features.Users.Query.GetUserDetails;
public class GetUserDetailQueryHandler(IUserRepository userRepository, IResumeRepository resumeRepository) : IRequestHandler<GetUserDetailQuery, Result<UserResponse>>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result<UserResponse>> Handle(GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdWithProfileAsync(request.userId, cancellationToken);
        if (user is null)
            return Result.Failure<UserResponse>(UserErrors.UserNotFound);

        var userRoles = await _userrepository.GetRolesAsync(user);
        var hasCv = await resumeRepository.HasCurrentAsync(user.Id, cancellationToken);
        var response = new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!,
            user.IsDisabled,
            userRoles,
            user.ProfilePhotoUrl,
            hasCv,
            user.CandidateProfile?.MinSalary,
            user.CandidateProfile?.ShowSalary ?? false);

        return Result.Success(response);
    }
}
