namespace WazzufJobs.BLL.Features.UserCV.Commands.DeleteCV;

public record DeleteCVCommand(string UserId) : IRequest<Result>;