using WazzufJobs.BLL.Contracts.Jobs;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.Jobs.Commands.CreateJob;

public class CreateJobCommandHandler(
    IJobRepository jobRepository,
    ICategoryRepository categoryRepository,
    ICompanyRepository companyRepository,
    IAuditLog audit
    ) : IRequestHandler<CreateJobCommand, Result<JobSummaryResponse>>
{
    private readonly IJobRepository _jobRepository = jobRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    // the change and its audit entry are committed together (Phase 02, S5)
    public async Task<Result<JobSummaryResponse>> Handle(CreateJobCommand request, CancellationToken cancellationToken) =>
        await audit.RecordAsync(() => ApplyAsync(request, cancellationToken), r => r.IsSuccess ? AuditEvent.Success(AuditActions.JobCreated).On(AuditTargets.Job, r.Value.Id.ToString()) : null, cancellationToken);

    private async Task<Result<JobSummaryResponse>> ApplyAsync(CreateJobCommand request, CancellationToken cancellationToken)
    {
        // validate category exists
        var category = await _categoryRepository
            .GetByIdAsync(request.Request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure<JobSummaryResponse>(JobErrors.CategoryNotFound);

        // Phase 02: jobs created by platform staff belong to the platform company (company job posting: P04).
        // The owner is looked up, never taken from input; if it is missing, nothing is created (fail closed).
        var platformCompanyId = await companyRepository.GetPlatformCompanyIdAsync(cancellationToken);
        if (platformCompanyId is null)
            return Result.Failure<JobSummaryResponse>(JobErrors.PlatformCompanyMissing);

        var job = new Job
        {
            CompanyId = platformCompanyId.Value,
            PublishedAt = DateTime.UtcNow,
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