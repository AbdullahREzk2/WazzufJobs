using WazzufJobs.BLL.Contracts.Applications;

namespace WazzufJobs.BLL.Features.Applications.Queries.GetApplicationDetail;
public record GetApplicationDetailQuery(int Id) : IRequest<Result<ApplicationDetailResponse>>;

