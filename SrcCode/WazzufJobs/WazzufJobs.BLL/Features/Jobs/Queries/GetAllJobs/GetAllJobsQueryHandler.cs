using WazzufJobs.BLL.Contracts.Common;
using WazzufJobs.BLL.Contracts.Jobs;


namespace WazzufJobs.BLL.Features.Jobs.Queries.GetAllJobs;

public class GetAllJobsQueryHandler(IJobRepository jobRepository) : IRequestHandler<GetAllJobsQuery, PaginatedResponse<JobSummaryResponse>>
{
    private readonly IJobRepository _jobRepository = jobRepository;

    public async Task<PaginatedResponse<JobSummaryResponse>> Handle(
        GetAllJobsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _jobRepository
            .GetAllAsync(request.Filter, cancellationToken);

        var mapped = items.Select(j => new JobSummaryResponse(
            j.Id,
            j.Title,
            j.Location,
            j.JobType.ToString(),
            j.WorkplaceType.ToString(),
            j.Category.Name,
            j.SalaryMin,
            j.SalaryMax,
            j.Status.ToString(),
            j.CreatedAt));

        return new PaginatedResponse<JobSummaryResponse>(
            mapped,
            totalCount,
            request.Filter.Page,
            request.Filter.PageSize);
    }
}