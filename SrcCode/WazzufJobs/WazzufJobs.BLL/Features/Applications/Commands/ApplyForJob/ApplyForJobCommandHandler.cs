using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.Applications.Commands.ApplyForJob;

public class ApplyForJobCommandHandler(
    IApplicationRepository applicationRepository,
    IJobRepository jobRepository,
    IUserRepository userRepository,
    IResumeRepository resumeRepository,
    IBackgroundJobClient backgroundJob)
    : IRequestHandler<ApplyForJobCommand, Result<int>>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly IJobRepository _jobRepository = jobRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IResumeRepository _resumeRepository = resumeRepository;
    private readonly IBackgroundJobClient _backgroundJob = backgroundJob;

    public async Task<Result<int>> Handle(ApplyForJobCommand request,CancellationToken cancellationToken)
    {
        // check job exists and is active
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);

        if (job is null)
            return Result.Failure<int>(JobErrors.NotFound);

        if (job.Status != JobStatus.Active)
            return Result.Failure<int>(ApplicationErrors.JobNotActive);

        var user = await _userRepository.FindByIdAsync(request.UserId);

        if (user is null)
            return Result.Failure<int>(UserErrors.UserNotFound);

        // the applicant's own current resume; the (ResumeId, CandidateId) foreign key also enforces ownership
        var resume = await _resumeRepository.GetCurrentAsync(request.UserId, includeText: false, cancellationToken);
        if (resume is null)
            return Result.Failure<int>(ApplicationErrors.CVNotFound);

        // check not already applied
        var hasApplied = await _applicationRepository.HasAppliedAsync(
            request.UserId, request.JobId, cancellationToken);

        if (hasApplied)
            return Result.Failure<int>(ApplicationErrors.AlreadyApplied);

        // create application
        // company and demo partition come from the job, never from input
        var application = new Application
        {
            UserId = request.UserId,
            JobId = job.Id,
            CompanyId = job.CompanyId,
            ResumeId = resume.Id,
            IsDemo = job.IsDemo,
            Status = ApplicationStatus.Pending,
            AppliedAt = DateTime.UtcNow
        };
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = null,
            ToStatus = ApplicationStatus.Pending,
            ChangedById = request.UserId,
            ChangedAt = application.AppliedAt
        });

        await _applicationRepository.AddAsync(application, cancellationToken);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

        // fire AI scoring job in background
        _backgroundJob.Enqueue<IAIScoringService>(x =>
            x.ScoreApplicationAsync(application.Id, CancellationToken.None));

        return Result.Success(application.Id);
    }
}
