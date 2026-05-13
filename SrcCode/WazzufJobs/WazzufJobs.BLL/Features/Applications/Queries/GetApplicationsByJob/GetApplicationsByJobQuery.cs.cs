using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Contracts.Common;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetApplicationsByJob;
public record GetApplicationsByJobQuery(int JobId, int Page = 1, int PageSize = 10) : IRequest<PaginatedResponse<ApplicationResponse>>;