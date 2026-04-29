namespace SurveyBasket.BLL.Features.Users.Command.UnlockUser;
public record UnlockUserCommand(string userId) : IRequest<Result>;
