using WazzufJobs.BLL.Contracts.SavedJobs;

namespace WazzufJobs.BLL.Features.SavedJobs.Queries.GetSavedJobs;

public class GetSavedJobsQueryHandler(ISavedJobRepository savedJobRepository): IRequestHandler<GetSavedJobsQuery, IEnumerable<SavedJobResponse>>
{
    private readonly ISavedJobRepository _savedJobRepository = savedJobRepository;

    public async Task<IEnumerable<SavedJobResponse>> Handle(GetSavedJobsQuery request,CancellationToken cancellationToken)
    {
        var savedJobs = await _savedJobRepository
            .GetByUserIdAsync(request.UserId, cancellationToken);

        return savedJobs.Select(s => new SavedJobResponse(
            s.JobId,
            s.Job.Title,
            s.Job.Location,
            s.Job.JobType.ToString(),
            s.Job.WorkplaceType.ToString(),
            s.Job.Category.Name,
            s.Job.SalaryMin,
            s.Job.SalaryMax,
            s.Job.Status.ToString(),
            s.SavedAt));
    }
}