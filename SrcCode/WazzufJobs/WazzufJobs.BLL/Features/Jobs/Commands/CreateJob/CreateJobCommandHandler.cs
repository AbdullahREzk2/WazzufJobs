using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Jobs;
using WazzufJobs.BLL.Errors;
using WazzufJobs.DAL.Enums;
using WazzufJobs.Shared;

namespace WazzufJobs.BLL.Features.Jobs.Commands.CreateJob;

public class CreateJobCommandHandler(
    IJobRepository jobRepository,
    ICategoryRepository categoryRepository
    ) : IRequestHandler<CreateJobCommand, Result<JobSummaryResponse>>
{
    private readonly IJobRepository _jobRepository = jobRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<Result<JobSummaryResponse>> Handle(CreateJobCommand request,CancellationToken cancellationToken)
    {
        // validate category exists
        var category = await _categoryRepository
            .GetByIdAsync(request.Request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure<JobSummaryResponse>(JobErrors.CategoryNotFound);

        var job = new Job
        {
            Title = request.Request.Title,
            Description = request.Request.Description,
            Location = request.Request.Location,
            Skills = request.Request.Skills,
            JobType = request.Request.JobType,
            WorkplaceType = request.Request.WorkplaceType,
            CategoryId = request.Request.CategoryId,
            SalaryMin = request.Request.SalaryMin,
            SalaryMax = request.Request.SalaryMax,
            ExpiresAt = request.Request.ExpiresAt,
            PostedById = request.UserId!,
            Status = JobStatus.Active
        };

        await _jobRepository.AddAsync(job, cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new JobSummaryResponse(
            job.Id,
            job.Title,
            job.Location,
            job.JobType.ToString(),
            job.WorkplaceType.ToString(),
            category.Name,
            job.SalaryMin,
            job.SalaryMax,
            job.Status.ToString(),
            job.CreatedAt));
    }
}