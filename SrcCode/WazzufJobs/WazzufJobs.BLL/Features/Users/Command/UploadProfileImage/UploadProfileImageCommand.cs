namespace SurveyBasket.BLL.Features.Users.Command.UploadProfileImage;
public record UploadProfileImageCommand(string userId, IFormFile image) : IRequest<Result<string>>;
