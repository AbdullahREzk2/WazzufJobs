using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.Jobs.Commands.ToggleJobStatus;

public class ToggleJobStatusCommandHandler(IJobRepository jobRepository, IAuditLog audit)
    : IRequestHandler<ToggleJobStatusCommand, Result>
{
    private readonly IJobRepository _jobRepository = jobRepository;
    private JobStatus? _status;

    // the change and its audit entry are committed together (Phase 02, S5)
    public async Task<Result> Handle(ToggleJobStatusCommand request, CancellationToken cancellationToken) =>
        await audit.RecordAsync(() => ApplyAsync(request, cancellationToken), r => r.IsSuccess ? AuditEvent.Success(AuditActions.JobStatusChanged).On(AuditTargets.Job, request.Id.ToString()).With(("to", _status)) : null, cancellationToken);

    private async Task<Result> ApplyAsync(ToggleJobStatusCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken);

        if (job is null)
            return Result.Failure(JobErrors.NotFound);

        job.Status = job.Status == JobStatus.Active
            ? JobStatus.Closed
            : JobStatus.Active;
        job.UpdatedAt = DateTime.UtcNow;
        _status = job.Status;
        if (job.Status == JobStatus.Active)
            job.PublishedAt ??= job.UpdatedAt;

        await _jobRepository.UpdateAsync(job);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}