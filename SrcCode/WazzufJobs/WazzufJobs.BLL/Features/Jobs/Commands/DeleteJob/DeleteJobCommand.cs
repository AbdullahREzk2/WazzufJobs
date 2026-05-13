namespace WazzufJobs.BLL.Features.Jobs.Commands.DeleteJob;

public record DeleteJobCommand(int Id) : IRequest<Result>;