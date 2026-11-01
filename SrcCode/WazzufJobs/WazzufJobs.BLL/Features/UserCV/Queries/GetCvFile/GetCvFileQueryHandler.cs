using WazzufJobs.BLL.Contracts.UserCV;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;

namespace WazzufJobs.BLL.Features.UserCV.Queries.GetCvFile;

public class GetCvFileQueryHandler(
    IResumeRepository resumeRepository,
    IApplicationRepository applicationRepository,
    IDocumentStorage documentStorage,
    IAuditLog audit)
    : IRequestHandler<GetCvFileQuery, Result<CvFileResponse>>,
      IRequestHandler<GetApplicationCvFileQuery, Result<CvFileResponse>>
{
    private readonly IResumeRepository _resumeRepository = resumeRepository;
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly IDocumentStorage _documentStorage = documentStorage;

    public async Task<Result<CvFileResponse>> Handle(GetCvFileQuery request, CancellationToken cancellationToken)
    {
        var resume = await _resumeRepository.GetCurrentAsync(request.UserId, includeText: false, cancellationToken);
        return resume is null
            ? Result.Failure<CvFileResponse>(CVErrors.NotFound)
            : await DownloadAsync(resume, cancellationToken);
    }

    public async Task<Result<CvFileResponse>> Handle(GetApplicationCvFileQuery request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application is null)
            return Result.Failure<CvFileResponse>(ApplicationErrors.NotFound);

        // C0 behaviour until resume versioning (P05): admins review the applicant's current CV
        var resume = await _resumeRepository.GetCurrentAsync(application.UserId, includeText: false, cancellationToken);
        if (resume is null)
            return Result.Failure<CvFileResponse>(CVErrors.NotFound);

        var file = await DownloadAsync(resume, cancellationToken);
        if (file.IsSuccess)   // required: no CV leaves the API unaudited (never the storage key or contents)
            await audit.WriteAsync(AuditEvent.Success(AuditActions.ApplicationCvAccessed)
                .On(AuditTargets.Application, application.Id.ToString()) with { CompanyId = application.CompanyId, IsDemo = application.IsDemo },
                cancellationToken);
        return file;
    }

    private async Task<Result<CvFileResponse>> DownloadAsync(DAL.Entities.Resume resume, CancellationToken cancellationToken)
    {
        var bytes = await _documentStorage.ReadAsync(
            resume.StorageKey,
            CvStorage.DeliveryType(resume.StorageAccess),
            cancellationToken);

        if (bytes is null || !CvStorage.HasPdfSignature(bytes))
            return Result.Failure<CvFileResponse>(CVErrors.FileUnavailable);

        var fileName = string.IsNullOrWhiteSpace(resume.FileName) ? "cv.pdf" : Path.GetFileName(resume.FileName);
        return Result.Success(new CvFileResponse(bytes, fileName, "application/pdf"));
    }
}
