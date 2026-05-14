using WazzufJobs.BLL.Contracts.UserCV;

namespace WazzufJobs.BLL.Features.UserCV.Queries.GetCV;
public record GetCVQuery(string UserId) : IRequest<Result<CVResponse>>;

