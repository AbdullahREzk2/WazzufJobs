using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Contracts.Common;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetApplicationsByJob;


public class GetApplicationsByJobQueryHandler(IApplicationRepository applicationRepository) : IRequestHandler<GetApplicationsByJobQuery, PaginatedResponse<ApplicationResponse>>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;

    public async Task<PaginatedResponse<ApplicationResponse>> Handle(GetApplicationsByJobQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _applicationRepository.GetByJobIdAsync(
            request.JobId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var mapped = items.Select(a => new ApplicationResponse(
            a.Id,
            $"{a.User.FirstName} {a.User.LastName}",
            a.User.Email!,
            a.Status.ToString(),
            a.AIScore,
            a.AIFeedback,
            a.IsAIScored,
            a.AppliedAt));

        return new PaginatedResponse<ApplicationResponse>(
            mapped,
            totalCount,
            request.Page,
            request.PageSize);
    }
}
