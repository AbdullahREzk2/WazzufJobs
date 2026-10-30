namespace WazzufJobs.BLL.Services;
public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

    /// <summary>Uploads a CV as an authenticated (non-public) raw asset. There is intentionally no public raw upload.</summary>
    Task<CloudinaryUploadResult> UploadPrivateFileAsync(Stream content, string folder, CancellationToken cancellationToken = default);

    /// <summary>Downloads a raw asset server-side through a short-lived signed API URL. Returns null on failure.</summary>
    Task<byte[]?> DownloadRawFileAsync(string publicId, string deliveryType, CancellationToken cancellationToken = default);

    Task<bool> DeleteRawFileAsync(string publicId, string deliveryType, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}
