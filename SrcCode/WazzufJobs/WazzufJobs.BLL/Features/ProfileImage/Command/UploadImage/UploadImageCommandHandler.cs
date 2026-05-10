using WazzufJobs.BLL.Services;

namespace WazzufJobs.BLL.Features.ProfileImage.Command.UploadImage;

public class UploadImageCommandHandler(ICloudinaryService cloudinaryService): IRequestHandler<UploadImageCommand, string?>
{
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<string?> Handle(
        UploadImageCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _cloudinaryService.UploadImageAsync(
            request.file,
            "wazzuf-jobs/profiles",  
            cancellationToken);

        return result.IsSuccess ? result.Url : null;
    }
}