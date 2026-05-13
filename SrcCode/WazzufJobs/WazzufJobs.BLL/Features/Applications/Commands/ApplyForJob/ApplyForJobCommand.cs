namespace WazzufJobs.BLL.Features.Applications.Commands.ApplyForJob;
public record ApplyForJobCommand(int JobId, string UserId) : IRequest<Result<int>>;
