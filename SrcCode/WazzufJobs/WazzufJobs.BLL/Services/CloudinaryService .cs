using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Setting;

namespace WazzufJobs.BLL.Services;

public class CloudinaryService : ICloudinaryService
{
    private const int MaxDownloadBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromSeconds(60);

    private readonly Cloudinary _cloudinary;
    private readonly IHttpClientFactory _httpClientFactory;

    public CloudinaryService(IOptions<CloudinarySettings> options, IHttpClientFactory httpClientFactory)
    {
        var settings = options.Value;
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
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

    // CVs are always stored as authenticated raw assets: they have no public delivery URL
    // and can only be retrieved by the backend through a short-lived signed API URL.
    public async Task<CloudinaryUploadResult> UploadPrivateFileAsync(Stream content, string folder, CancellationToken cancellationToken = default)
    {
        var uploadParams = BuildPrivateRawUploadParams(content, folder);

        var result = await _cloudinary.UploadLargeRawAsync(uploadParams);

        if (result.Error is not null)
            return new CloudinaryUploadResult(null, null, false);

        // Defence in depth: never accept a stored asset that is not authenticated.
        if (!string.Equals(result.Type, CvStorage.AuthenticatedType, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteRawFileAsync(result.PublicId, result.Type ?? CvStorage.PublicUploadType, cancellationToken);
            return new CloudinaryUploadResult(null, null, false);
        }

        return new CloudinaryUploadResult(
            result.SecureUrl?.ToString(),
            result.PublicId,
            true);
    }

    public async Task<byte[]?> DownloadRawFileAsync(string publicId, string deliveryType, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(SignedUrlLifetime).ToUnixTimeSeconds();

        var signedUrl = _cloudinary.DownloadPrivate(
            publicId,
            attachment: true,
            format: string.Empty,
            type: deliveryType,
            expiresAt: expiresAt,
            resourceType: CvStorage.RawResourceType);

        var client = _httpClientFactory.CreateClient(nameof(CloudinaryService));
        using var response = await client.GetAsync(signedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        if (response.Content.Headers.ContentLength is > MaxDownloadBytes)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.Length > MaxDownloadBytes ? null : bytes;
    }

    public async Task<bool> DeleteRawFileAsync(string publicId, string deliveryType, CancellationToken cancellationToken = default)
    {
        var deleteParams = new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Raw,
            Type = deliveryType,
            Invalidate = true
        };

        var result = await _cloudinary.DestroyAsync(deleteParams);
        return result.Result == "ok";
    }

    public async Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var deleteParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deleteParams);
        return result.Result == "ok";
    }

    public static RawUploadParams BuildPrivateRawUploadParams(Stream content, string folder) => new()
    {
        // The original client filename is never sent: Cloudinary generates a random public id.
        File = new FileDescription("cv.pdf", content),
        Folder = folder,
        Type = CvStorage.AuthenticatedType,
        UseFilename = false,
        UniqueFilename = true,
        Overwrite = false
    };
}
