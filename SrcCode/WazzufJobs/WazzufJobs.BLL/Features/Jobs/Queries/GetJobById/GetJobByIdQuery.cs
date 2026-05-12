using WazzufJobs.BLL.Contracts.Jobs;

namespace WazzufJobs.BLL.Features.Jobs.Queries.GetJobById;
public record GetJobByIdQuery(int Id) : IRequest<Result<JobResponse>>;

