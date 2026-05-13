using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.Jobs.Commands.ToggleJobStatus;

public class ToggleJobStatusCommandHandler(IJobRepository jobRepository)
    : IRequestHandler<ToggleJobStatusCommand, Result>
{
    private readonly IJobRepository _jobRepository = jobRepository;

    public async Task<Result> Handle(
        ToggleJobStatusCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken);

        if (job is null)
            return Result.Failure(JobErrors.NotFound);

        job.Status = job.Status == JobStatus.Active
            ? JobStatus.Closed
            : JobStatus.Active;

        await _jobRepository.UpdateAsync(job);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}