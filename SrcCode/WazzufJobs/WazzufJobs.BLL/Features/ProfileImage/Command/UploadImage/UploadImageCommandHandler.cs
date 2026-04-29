using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;

namespace WazzufJobs.BLL.Features.ProfileImage.Command.UploadImage;

public class UploadImageCommandHandler : IRequestHandler<UploadImageCommand, string?>
{
    private readonly Cloudinary _cloudinary;

    public UploadImageCommandHandler(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string?> Handle(UploadImageCommand request, CancellationToken cancellationToken)
    {
        using var stream = request.file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(request.file.FileName, stream),
            Folder = "survey-basket/profiles",
            Transformation = new Transformation().Width(500).Height(500).Crop("fill")
        };
        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
        return result.Error is null ? result.SecureUrl.ToString() : null;
    }
}