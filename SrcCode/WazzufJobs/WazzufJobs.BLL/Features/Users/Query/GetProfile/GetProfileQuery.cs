namespace SurveyBasket.BLL.Features.Users.Query.GetProfile;
public record GetProfileQuery(string userId) : IRequest<Result<UserProfileResponse>>;
