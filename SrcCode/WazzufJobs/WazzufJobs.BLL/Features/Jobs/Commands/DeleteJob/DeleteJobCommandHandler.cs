namespace WazzufJobs.BLL.Features.Jobs.Commands.DeleteJob;

public class DeleteJobCommandHandler(IJobRepository jobRepository, IAuditLog audit)
    : IRequestHandler<DeleteJobCommand, Result>
{
    private readonly IJobRepository _jobRepository = jobRepository;

    // the change and its audit entry are committed together (Phase 02, S5)
    public async Task<Result> Handle(DeleteJobCommand request, CancellationToken cancellationToken) =>
        await audit.RecordAsync(() => ApplyAsync(request, cancellationToken), r => r.IsSuccess ? AuditEvent.Success(AuditActions.JobDeleted).On(AuditTargets.Job, request.Id.ToString()) : null, cancellationToken);

    private async Task<Result> ApplyAsync(DeleteJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken);

        if (job is null)
            return Result.Failure(JobErrors.NotFound);

        await _jobRepository.DeleteAsync(job);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}