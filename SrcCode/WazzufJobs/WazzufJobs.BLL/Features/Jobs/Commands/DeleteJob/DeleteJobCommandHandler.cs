namespace WazzufJobs.BLL.Features.Jobs.Commands.DeleteJob;

public class DeleteJobCommandHandler(IJobRepository jobRepository)
    : IRequestHandler<DeleteJobCommand, Result>
{
    private readonly IJobRepository _jobRepository = jobRepository;

    public async Task<Result> Handle(
        DeleteJobCommand request,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken);

        if (job is null)
            return Result.Failure(JobErrors.NotFound);

        await _jobRepository.DeleteAsync(job);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}