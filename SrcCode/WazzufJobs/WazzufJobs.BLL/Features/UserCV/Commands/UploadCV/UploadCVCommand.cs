using WazzufJobs.BLL.Contracts.UserCV;

namespace WazzufJobs.BLL.Features.UserCV.Commands.UploadCV;
public record UploadCVCommand(string UserId,IFormFile File) : IRequest<Result<CVResponse>>;