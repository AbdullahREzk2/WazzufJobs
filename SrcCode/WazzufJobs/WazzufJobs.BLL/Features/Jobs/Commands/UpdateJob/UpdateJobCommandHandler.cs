namespace WazzufJobs.BLL.Features.Jobs.Commands.UpdateJob;

public class UpdateJobCommandHandler(
    IJobRepository jobRepository,
    ICategoryRepository categoryRepository)
    : IRequestHandler<UpdateJobCommand, Result>
{
    private readonly IJobRepository _jobRepository = jobRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<Result> Handle(UpdateJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken);

        if (job is null)
            return Result.Failure(JobErrors.NotFound);

        var category = await _categoryRepository
            .GetByIdAsync(request.Request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure(JobErrors.CategoryNotFound);

        job.Title = request.Request.Title;
        job.Description = request.Request.Description;
        job.Location = request.Request.Location;
        job.Skills = request.Request.Skills;
        job.JobType = request.Request.JobType;
        job.WorkplaceType = request.Request.WorkplaceType;
        job.CategoryId = request.Request.CategoryId;
        job.SalaryMin = request.Request.SalaryMin;
        job.SalaryMax = request.Request.SalaryMax;
        job.ExpiresAt = request.Request.ExpiresAt;

        await _jobRepository.UpdateAsync(job);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}