namespace WazzufJobs.BLL.Services;
public interface ICloudinaryService
{

    Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file,string folder,CancellationToken cancellationToken = default);

    Task<CloudinaryUploadResult> UploadFileAsync(IFormFile file,string folder,CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string publicId,CancellationToken cancellationToken = default);
}
