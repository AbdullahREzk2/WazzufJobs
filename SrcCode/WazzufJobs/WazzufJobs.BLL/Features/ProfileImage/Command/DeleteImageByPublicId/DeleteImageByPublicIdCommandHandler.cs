using WazzufJobs.BLL.Services;

namespace WazzufJobs.BLL.Features.ProfileImage.Command.DeleteImageByPublicId;

public class DeleteImageByPublicIdCommandHandler(ICloudinaryService cloudinaryService) : IRequestHandler<DeleteImageByPublicIdCommand, Unit>
{
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<Unit> Handle(
        DeleteImageByPublicIdCommand request,
        CancellationToken cancellationToken)
    {
        await _cloudinaryService.DeleteAsync(request.publicId, cancellationToken);
        return Unit.Value;
    }
}