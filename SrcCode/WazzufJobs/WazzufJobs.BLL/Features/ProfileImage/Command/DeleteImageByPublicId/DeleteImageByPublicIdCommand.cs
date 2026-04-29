namespace WazzufJobs.BLL.Features.ProfileImage.Command.DeleteImageByPublicId;
public record DeleteImageByPublicIdCommand(string publicId) : IRequest<Unit>;
