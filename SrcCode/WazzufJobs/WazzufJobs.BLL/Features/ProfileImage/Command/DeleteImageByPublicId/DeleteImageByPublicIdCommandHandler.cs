using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Features.ProfileImage.Command.DeleteImageByPublicId;
using WazzufJobs.BLL.Setting;

namespace SurveyBasket.BLL.Features.ProfileImage.Command.DeleteImageByPublicId;

public class DeleteImageByPublicIdCommandHandler : IRequestHandler<DeleteImageByPublicIdCommand, Unit>
{
    private readonly Cloudinary _cloudinary;

    public DeleteImageByPublicIdCommandHandler(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<Unit> Handle(DeleteImageByPublicIdCommand request, CancellationToken cancellationToken)
    {
        var deleteParams = new DeletionParams(request.publicId);
        await _cloudinary.DestroyAsync(deleteParams);
        return Unit.Value;
    }
}