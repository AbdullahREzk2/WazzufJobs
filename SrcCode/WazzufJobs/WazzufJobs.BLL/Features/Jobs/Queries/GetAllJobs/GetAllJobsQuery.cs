using WazzufJobs.BLL.Contracts.Common;
using WazzufJobs.BLL.Contracts.Jobs;
using WazzufJobs.DAL.DTOS;

namespace WazzufJobs.BLL.Features.Jobs.Queries.GetAllJobs;
public record GetAllJobsQuery(JobFilterRequestDTO Filter): IRequest<PaginatedResponse<JobSummaryResponse>>;
