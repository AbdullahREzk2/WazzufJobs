
namespace WazzufJobs.BLL.Features.SavedJobs.Commands.SaveJob;

public record SaveJobCommand(string UserId,int JobId) : IRequest<Result>;