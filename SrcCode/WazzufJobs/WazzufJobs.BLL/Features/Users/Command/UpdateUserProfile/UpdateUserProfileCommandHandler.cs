namespace SurveyBasket.BLL.Features.Users.Command.UpdateUserProfile;
public class UpdateUserProfileCommandHandler(IUserRepository userRepository) : IRequestHandler<UpdateUserProfileCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        await _userrepository.UpdateUserProfileAsync(request.userId, request.Profilerequest.firstName, request.Profilerequest.lastName);
        return Result.Success();
    }
}
