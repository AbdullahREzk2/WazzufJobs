using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Jobs;
using WazzufJobs.BLL.Errors;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.Jobs.Queries.GetJobById;

public class GetJobByIdQueryHandler(IJobRepository jobRepository): IRequestHandler<GetJobByIdQuery, Result<JobResponse>>
{
    private readonly IJobRepository _jobRepository = jobRepository;

    public async Task<Result<JobResponse>> Handle(GetJobByIdQuery request,CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken);

        if (job is null)
            return Result.Failure<JobResponse>(JobErrors.NotFound);

        return Result.Success(new JobResponse(
            job.Id,
            job.Title,
            job.Description,
            job.Location,
            job.Skills,
            job.JobType.ToString(),
            job.WorkplaceType.ToString(),
            job.Category.Name,
            $"{job.PostedBy.FirstName} {job.PostedBy.LastName}",
            job.SalaryMin,
            job.SalaryMax,
            job.Status.ToString(),
            job.CreatedAt,
            job.ExpiresAt));
    }
}