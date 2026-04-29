namespace WazzufJobs.BLL.Features.ProfileImage.Command.UploadImage;
public record UploadImageCommand(IFormFile file) : IRequest<string?>;
