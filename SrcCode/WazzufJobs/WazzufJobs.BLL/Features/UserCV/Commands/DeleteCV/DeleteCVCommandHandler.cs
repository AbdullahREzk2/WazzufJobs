using Microsoft.Extensions.Logging;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Features.UserCV.Commands.DeleteCV;

/// <summary>
/// Deletes the candidate's current CV (Phase 02 S2b). The file is removed exactly as in C0. The resume row is deleted,
/// unless an application references it: then it stays as a "removed" record without a file, so the application keeps
/// a valid reference and the file can no longer be downloaded.
/// </summary>
public class DeleteCVCommandHandler(
    IResumeRepository resumeRepository,
    IDocumentStorage documentStorage,
    ILogger<DeleteCVCommandHandler> logger): IRequestHandler<DeleteCVCommand, Result>
{
    private readonly IResumeRepository _resumeRepository = resumeRepository;
    private readonly IDocumentStorage _documentStorage = documentStorage;
    private readonly ILogger<DeleteCVCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteCVCommand request,CancellationToken cancellationToken)
    {
        var resume = await _resumeRepository.GetCurrentAsync(request.UserId, includeText: true, cancellationToken);
        if (resume is null)
            return Result.Failure(CVErrors.NotFound);

        if (!string.IsNullOrEmpty(resume.StorageKey))
        {
            if (CvStorage.IsLegacyPublic(resume.StorageAccess))
            {
                // Legacy public assets are retained until the approved C0 remediation handles them.
                _logger.LogWarning(
                    "LegacyCvAssetRetained: user {UserId} deleted a legacy public CV asset {PublicId}; awaiting approved remediation.",
                    request.UserId, resume.StorageKey);
            }
            else if (!await _documentStorage.DeleteAsync(resume.StorageKey, CvStorage.AuthenticatedType, cancellationToken))
            {
                _logger.LogWarning("CvAssetDeleteFailed: authenticated CV asset {PublicId} could not be deleted.", resume.StorageKey);
            }
        }

        if (await _resumeRepository.IsReferencedAsync(resume.Id, cancellationToken))
        {
            resume.IsDefault = false;
            resume.Status = ResumeStatus.Removed;
            if (resume.Text is not null)
                resume.Text.ExtractedText = string.Empty;
        }
        else
        {
            _resumeRepository.Remove(resume);
        }

        await _resumeRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
