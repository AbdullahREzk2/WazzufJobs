using WazzufJobs.BLL.Contracts.Applications;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetApplicationDetail;

public class GetApplicationDetailQueryHandler(IApplicationRepository applicationRepository, IResumeRepository resumeRepository) : IRequestHandler<GetApplicationDetailQuery, Result<ApplicationDetailResponse>>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;

    public async Task<Result<ApplicationDetailResponse>> Handle(GetApplicationDetailQuery request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository
            .GetByIdWithDetailsAsync(request.Id, cancellationToken);

        if (application is null)
            return Result.Failure<ApplicationDetailResponse>(ApplicationErrors.NotFound);

        // C0 behaviour until resume versioning (P05): "has CV" refers to the applicant's current CV
        var hasCv = await resumeRepository.HasCurrentAsync(application.UserId, cancellationToken);
        var profile = application.User.CandidateProfile;

        return Result.Success(new ApplicationDetailResponse(
            application.Id,
            application.UserId,
            $"{application.User.FirstName} {application.User.LastName}",
            application.User.Email!,
            application.User.ProfilePhotoUrl,
            hasCv,
            application.Job.Title,
            application.Status.ToString(),
            application.AIScore,
            application.AIFeedback,
            application.IsAIScored,
            application.AppliedAt,
            profile?.MinSalary,
            profile?.ShowSalary ?? false));
    }
}
