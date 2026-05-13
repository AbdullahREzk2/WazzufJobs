using WazzufJobs.BLL.Contracts.Applications;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetApplicationDetail;

public class GetApplicationDetailQueryHandler(IApplicationRepository applicationRepository) : IRequestHandler<GetApplicationDetailQuery, Result<ApplicationDetailResponse>>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;

    public async Task<Result<ApplicationDetailResponse>> Handle(GetApplicationDetailQuery request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository
            .GetByIdWithDetailsAsync(request.Id, cancellationToken);

        if (application is null)
            return Result.Failure<ApplicationDetailResponse>(ApplicationErrors.NotFound);

        return Result.Success(new ApplicationDetailResponse(
            application.Id,
            $"{application.User.FirstName} {application.User.LastName}",
            application.User.Email!,
            application.User.CV?.Url ?? string.Empty,
            application.Job.Title,
            application.Status.ToString(),
            application.AIScore,
            application.AIFeedback,
            application.IsAIScored,
            application.AppliedAt));
    }
}
