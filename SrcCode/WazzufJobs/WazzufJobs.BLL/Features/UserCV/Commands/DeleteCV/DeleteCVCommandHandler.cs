using Microsoft.Extensions.Logging;
using WazzufJobs.BLL.Services;

namespace WazzufJobs.BLL.Features.UserCV.Commands.DeleteCV;

public class DeleteCVCommandHandler(
    ICVRepository cvRepository,
    ICloudinaryService cloudinaryService,
    ILogger<DeleteCVCommandHandler> logger): IRequestHandler<DeleteCVCommand, Result>
{
    private readonly ICVRepository _cvRepository = cvRepository;
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;
    private readonly ILogger<DeleteCVCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteCVCommand request,CancellationToken cancellationToken)
    {
        var cv = await _cvRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (cv is null)
            return Result.Failure(CVErrors.NotFound);

        if (!string.IsNullOrEmpty(cv.PublicId))
        {
            if (CvStorage.IsLegacyPublic(cv.Url))
            {
                // Legacy public assets are retained until the approved C0 remediation handles them.
                _logger.LogWarning(
                    "LegacyCvAssetRetained: user {UserId} deleted a legacy public CV asset {PublicId}; awaiting approved remediation.",
                    request.UserId, cv.PublicId);
            }
            else if (!await _cloudinaryService.DeleteRawFileAsync(cv.PublicId, CvStorage.AuthenticatedType, cancellationToken))
            {
                _logger.LogWarning("CvAssetDeleteFailed: authenticated CV asset {PublicId} could not be deleted.", cv.PublicId);
            }
        }

        await _cvRepository.DeleteAsync(cv);
        await _cvRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
