namespace WazzufJobs.BLL.Features.SavedJobs.Commands.RemoveSavedJob;

public class RemoveSavedJobCommandHandler(ISavedJobRepository savedJobRepository): IRequestHandler<RemoveSavedJobCommand, Result>
{
    private readonly ISavedJobRepository _savedJobRepository = savedJobRepository;

    public async Task<Result> Handle(
        RemoveSavedJobCommand request,
        CancellationToken cancellationToken)
    {
        var savedJob = await _savedJobRepository.GetAsync(
            request.UserId, request.JobId, cancellationToken);

        if (savedJob is null)
            return Result.Failure(SavedJobErrors.NotFound);

        await _savedJobRepository.DeleteAsync(savedJob);
        await _savedJobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}