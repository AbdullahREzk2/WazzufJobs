using WazzufJobs.BLL.Contracts.SavedJobs;

namespace WazzufJobs.BLL.Features.SavedJobs.Queries.GetSavedJobs;

public record GetSavedJobsQuery(string UserId): IRequest<IEnumerable<SavedJobResponse>>;