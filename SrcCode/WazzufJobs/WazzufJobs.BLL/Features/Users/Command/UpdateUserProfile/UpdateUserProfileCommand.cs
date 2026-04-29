namespace SurveyBasket.BLL.Features.Users.Command.UpdateUserProfile;
public record UpdateUserProfileCommand(string userId, UpdateProfileRequest Profilerequest) : IRequest<Result>;
