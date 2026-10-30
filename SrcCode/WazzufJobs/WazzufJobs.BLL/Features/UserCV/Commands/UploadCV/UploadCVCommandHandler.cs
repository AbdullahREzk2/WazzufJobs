// WazzufJobs.BLL/Features/UserCV/Commands/UploadCV/UploadCVCommandHandler.cs
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.UserCV;
using WazzufJobs.BLL.Errors;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;
using WazzufJobs.BLL.Setting;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.UserCV.Commands.UploadCV;

public class UploadCVCommandHandler(
    ICVRepository cvRepository,
    IDocumentStorage documentStorage,
    ICVTextExtractor cvTextExtractor,
    IOptions<FeatureSettings> features,
    ILogger<UploadCVCommandHandler> logger)
    : IRequestHandler<UploadCVCommand, Result<CVResponse>>
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly ICVRepository _cvRepository = cvRepository;
    private readonly IDocumentStorage _documentStorage = documentStorage;
    private readonly ICVTextExtractor _cvTextExtractor = cvTextExtractor;
    private readonly FeatureSettings _features = features.Value;
    private readonly ILogger<UploadCVCommandHandler> _logger = logger;

    public async Task<Result<CVResponse>> Handle(
        UploadCVCommand request,
        CancellationToken cancellationToken)
    {
        // Phase C0: uploads are off until authenticated storage is verified. Never fall back to public storage.
        if (!_features.CvUploadsEnabled)
            return Result.Failure<CVResponse>(CVErrors.UploadsDisabled);

        if (!request.File.ContentType.Equals("application/pdf",
                StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CVResponse>(CVErrors.InvalidFileType);

        if (request.File.Length > MaxFileBytes)
            return Result.Failure<CVResponse>(CVErrors.FileTooLarge);

        using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        var pdfBytes = memoryStream.ToArray();

        // the declared content type is client-controlled; check the actual file signature
        if (!CvStorage.HasPdfSignature(pdfBytes))
            return Result.Failure<CVResponse>(CVErrors.InvalidFileType);

        var extractedText = _cvTextExtractor.ExtractFromBytes(pdfBytes);

        if (string.IsNullOrWhiteSpace(extractedText))
            return Result.Failure<CVResponse>(CVErrors.ScannedPDF);

        memoryStream.Position = 0;
        var stored = await _documentStorage.StorePrivateAsync(
            memoryStream,
            CvStorage.CvFolder,
            cancellationToken);

        if (stored is null)
            return Result.Failure<CVResponse>(CVErrors.UploadFailed);

        var fileName = SanitizeFileName(request.File.FileName);
        var existingCV = await _cvRepository.GetByUserIdAsync(
            request.UserId, cancellationToken);

        if (existingCV is not null)
        {
            var previousPublicId = existingCV.PublicId;
            var previousUrl = existingCV.Url;

            existingCV.Url = stored.ProviderReference;
            existingCV.PublicId = stored.Key;
            existingCV.FileName = fileName;
            existingCV.ExtractedText = extractedText;
            existingCV.UploadedAt = DateTime.UtcNow;

            await _cvRepository.UpdateAsync(existingCV);
            await _cvRepository.SaveChangesAsync(cancellationToken);

            await RemovePreviousAssetAsync(request.UserId, previousPublicId, previousUrl, cancellationToken);

            return Result.Success(new CVResponse(
                existingCV.Id,
                existingCV.FileName,
                existingCV.UploadedAt));
        }

        var cv = new CV
        {
            UserId = request.UserId,
            Url = stored.ProviderReference,
            PublicId = stored.Key,
            FileName = fileName,
            ExtractedText = extractedText,
            UploadedAt = DateTime.UtcNow
        };

        await _cvRepository.AddAsync(cv, cancellationToken);
        await _cvRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new CVResponse(
            cv.Id,
            cv.FileName,
            cv.UploadedAt));
    }

    private async Task RemovePreviousAssetAsync(string userId, string publicId, string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(publicId))
            return;

        // Legacy public assets are retained until the approved C0 remediation handles them.
        if (CvStorage.IsLegacyPublic(url))
        {
            _logger.LogWarning(
                "LegacyCvAssetRetained: user {UserId} replaced a legacy public CV asset {PublicId}; awaiting approved remediation.",
                userId, publicId);
            return;
        }

        var deleted = await _documentStorage.DeleteAsync(publicId, CvStorage.AuthenticatedType, cancellationToken);
        if (!deleted)
            _logger.LogWarning("CvAssetDeleteFailed: authenticated CV asset {PublicId} could not be deleted.", publicId);
    }

    private static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ').ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "cv.pdf";

        if (!cleaned.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            cleaned += ".pdf";

        return cleaned.Length > 255 ? cleaned[^255..] : cleaned;
    }
}
