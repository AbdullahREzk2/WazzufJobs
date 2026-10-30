namespace WazzufJobs.BLL.Services;

/// <summary>Public image hosting. CV documents never use this service (see Storage.IDocumentStorage).</summary>
public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}
