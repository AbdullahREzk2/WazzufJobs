using WazzufJobs.BLL.Contracts.Jobs;

namespace WazzufJobs.BLL.Features.Jobs.Commands.CreateJob;
public record CreateJobCommand(JobRequest Request, string UserId) : IRequest<Result<JobSummaryResponse>>;
