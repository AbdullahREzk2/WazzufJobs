namespace WazzufJobs.BLL.Features.Jobs.Commands.ToggleJobStatus;

public record ToggleJobStatusCommand(int Id) : IRequest<Result>;