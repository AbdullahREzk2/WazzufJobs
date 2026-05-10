namespace WazzufJobs.BLL.Services;
public record CloudinaryUploadResult(
    string? Url,
    string? PublicId,
    bool IsSuccess);