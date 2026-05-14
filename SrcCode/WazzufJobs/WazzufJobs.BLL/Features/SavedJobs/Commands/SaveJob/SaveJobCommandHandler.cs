namespace WazzufJobs.BLL.Features.SavedJobs.Commands.SaveJob;

public class SaveJobCommandHandler(
    ISavedJobRepository savedJobRepository,
    IJobRepository jobRepository)
    : IRequestHandler<SaveJobCommand, Result>
{
    private readonly ISavedJobRepository _savedJobRepository = savedJobRepository;
    private readonly IJobRepository _jobRepository = jobRepository;

    public async Task<Result> Handle(SaveJobCommand request,CancellationToken cancellationToken)
    {
        // check job exists
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);

        if (job is null)
            return Result.Failure(SavedJobErrors.JobNotFound);

        // check not already saved
        var isSaved = await _savedJobRepository.IsSavedAsync(
            request.UserId, request.JobId, cancellationToken);

        if (isSaved)
            return Result.Failure(SavedJobErrors.AlreadySaved);

        var savedJob = new SavedJob
        {
            UserId = request.UserId,
            JobId = request.JobId,
            SavedAt = DateTime.UtcNow
        };

        await _savedJobRepository.AddAsync(savedJob, cancellationToken);
        await _savedJobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}