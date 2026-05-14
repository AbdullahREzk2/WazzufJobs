using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Contracts.Common;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetMyApplications;

public class GetMyApplicationsQueryHandler(IApplicationRepository applicationRepository)
    : IRequestHandler<GetMyApplicationsQuery, PaginatedResponse<MyApplicationResponse>>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;

    public async Task<PaginatedResponse<MyApplicationResponse>> Handle(
        GetMyApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _applicationRepository.GetByUserIdAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var mapped = items.Select(a => new MyApplicationResponse(
            a.Id,
            a.Job.Title,
            a.Job.Location,
            a.Job.Category.Name,
            a.Status.ToString(),
            a.AIScore,
            a.AIFeedback,
            a.IsAIScored,
            a.AppliedAt));

        return new PaginatedResponse<MyApplicationResponse>(
            mapped,
            totalCount,
            request.Page,
            request.PageSize);
    }
}