using WazzufJobs.BLL.Contracts.UserCV;
using WazzufJobs.BLL.Services;

namespace WazzufJobs.BLL.Features.UserCV.Queries.GetCvFile;

public class GetCvFileQueryHandler(
    ICVRepository cvRepository,
    IApplicationRepository applicationRepository,
    ICloudinaryService cloudinaryService)
    : IRequestHandler<GetCvFileQuery, Result<CvFileResponse>>,
      IRequestHandler<GetApplicationCvFileQuery, Result<CvFileResponse>>
{
    private readonly ICVRepository _cvRepository = cvRepository;
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<Result<CvFileResponse>> Handle(GetCvFileQuery request, CancellationToken cancellationToken)
    {
        var cv = await _cvRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return cv is null
            ? Result.Failure<CvFileResponse>(CVErrors.NotFound)
            : await DownloadAsync(cv, cancellationToken);
    }

    public async Task<Result<CvFileResponse>> Handle(GetApplicationCvFileQuery request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository.GetByIdWithDetailsAsync(request.ApplicationId, cancellationToken);

        if (application is null)
            return Result.Failure<CvFileResponse>(ApplicationErrors.NotFound);

        var cv = application.User.CV;

        return cv is null
            ? Result.Failure<CvFileResponse>(CVErrors.NotFound)
            : await DownloadAsync(cv, cancellationToken);
    }

    private async Task<Result<CvFileResponse>> DownloadAsync(CV cv, CancellationToken cancellationToken)
    {
        var bytes = await _cloudinaryService.DownloadRawFileAsync(
            cv.PublicId,
            CvStorage.DeliveryTypeFromUrl(cv.Url),
            cancellationToken);

        if (bytes is null || !CvStorage.HasPdfSignature(bytes))
            return Result.Failure<CvFileResponse>(CVErrors.FileUnavailable);

        var fileName = string.IsNullOrWhiteSpace(cv.FileName) ? "cv.pdf" : Path.GetFileName(cv.FileName);

        return Result.Success(new CvFileResponse(bytes, fileName, "application/pdf"));
    }
}
