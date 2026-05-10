using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;

namespace WazzufJobs.BLL.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file,string folder,CancellationToken cancellationToken = default)
    {
        using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder,
            Transformation = new Transformation()
                .Width(500).Height(500).Crop("fill")
        };

        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

        if (result.Error is not null)
            return new CloudinaryUploadResult(null, null, false);

        return new CloudinaryUploadResult(
            result.SecureUrl.ToString(),
            result.PublicId,
            true);
    }

    public async Task<CloudinaryUploadResult> UploadFileAsync(IFormFile file,string folder,CancellationToken cancellationToken = default)
    {
        using var stream = file.OpenReadStream();

        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder
        };

        var result = await _cloudinary.UploadLargeRawAsync(uploadParams);  

        if (result.Error is not null)
            return new CloudinaryUploadResult(null, null, false);

        return new CloudinaryUploadResult(
            result.SecureUrl.ToString(),
            result.PublicId,
            true);
    }

    public async Task<bool> DeleteAsync(string publicId,CancellationToken cancellationToken = default)
    {
        var deleteParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deleteParams);
        return result.Result == "ok";
    }


}