namespace SurveyBasket.BLL.Features.Users.Query.GetUserDetails;
public record GetUserDetailQuery(string userId) : IRequest<Result<UserResponse>>;
