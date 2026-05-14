using WazzufJobs.BLL.Contracts.Applications;
using WazzufJobs.BLL.Contracts.Common;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetMyApplications;

public record GetMyApplicationsQuery(
    string UserId,
    int Page = 1,
    int PageSize = 10) : IRequest<PaginatedResponse<MyApplicationResponse>>;