using WazzufJobs.BLL.Contracts.UserCV;
using WazzufJobs.BLL.Services;

namespace WazzufJobs.BLL.Features.UserCV.Commands.UploadCV;

public class UploadCVCommandHandler(
    ICVRepository cvRepository,
    ICloudinaryService cloudinaryService): IRequestHandler<UploadCVCommand, Result<CVResponse>>
{
    private readonly ICVRepository _cvRepository = cvRepository;
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<Result<CVResponse>> Handle(
        UploadCVCommand request,
        CancellationToken cancellationToken)
    {
        // validate file type
        if (!request.File.ContentType.Equals("application/pdf",
                StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CVResponse>(CVErrors.InvalidFileType);

        // validate file size (5MB max)
        if (request.File.Length > 5 * 1024 * 1024)
            return Result.Failure<CVResponse>(CVErrors.FileTooLarge);

        // check if user already has a CV
        var existingCV = await _cvRepository.GetByUserIdAsync(
            request.UserId, cancellationToken);

        // delete old CV from Cloudinary if exists
        if (existingCV is not null && !string.IsNullOrEmpty(existingCV.PublicId))
            await _cloudinaryService.DeleteAsync(existingCV.PublicId, cancellationToken);

        // upload new CV to Cloudinary
        var uploadResult = await _cloudinaryService.UploadFileAsync(
            request.File,
            "wazzuf-jobs/cvs",
            cancellationToken);

        if (!uploadResult.IsSuccess)
            return Result.Failure<CVResponse>(CVErrors.UploadFailed);

        if (existingCV is not null)
        {
            // update existing
            existingCV.Url = uploadResult.Url!;
            existingCV.PublicId = uploadResult.PublicId!;
            existingCV.FileName = request.File.FileName;
            existingCV.UploadedAt = DateTime.UtcNow;

            await _cvRepository.UpdateAsync(existingCV);
            await _cvRepository.SaveChangesAsync(cancellationToken);

            return Result.Success(new CVResponse(
                existingCV.Id,
                existingCV.Url,
                existingCV.FileName,
                existingCV.UploadedAt));
        }

        // create new
        var cv = new CV
        {
            UserId = request.UserId,
            Url = uploadResult.Url!,
            PublicId = uploadResult.PublicId!,
            FileName = request.File.FileName,
            UploadedAt = DateTime.UtcNow
        };

        await _cvRepository.AddAsync(cv, cancellationToken);
        await _cvRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new CVResponse(
            cv.Id,
            cv.Url,
            cv.FileName,
            cv.UploadedAt));
    }
}