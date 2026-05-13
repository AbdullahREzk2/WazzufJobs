namespace WazzufJobs.BLL.Features.Applications.Commands.UpdateApplicationStatus;

public class UpdateApplicationStatusCommandHandler(IApplicationRepository applicationRepository) : IRequestHandler<UpdateApplicationStatusCommand, Result>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;

    public async Task<Result> Handle(UpdateApplicationStatusCommand request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository
            .GetByIdAsync(request.Id, cancellationToken);

        if (application is null)
            return Result.Failure(ApplicationErrors.NotFound);

        application.Status = request.Request.Status;

        await _applicationRepository.UpdateAsync(application);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}