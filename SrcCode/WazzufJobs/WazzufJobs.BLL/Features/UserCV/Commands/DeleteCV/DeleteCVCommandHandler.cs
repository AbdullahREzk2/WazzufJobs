using WazzufJobs.BLL.Services;

namespace WazzufJobs.BLL.Features.UserCV.Commands.DeleteCV;

public class DeleteCVCommandHandler(
    ICVRepository cvRepository,
    ICloudinaryService cloudinaryService): IRequestHandler<DeleteCVCommand, Result>
{
    private readonly ICVRepository _cvRepository = cvRepository;
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<Result> Handle(DeleteCVCommand request,CancellationToken cancellationToken)
    {
        var cv = await _cvRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (cv is null)
            return Result.Failure(CVErrors.NotFound);

        // delete from Cloudinary
        if (!string.IsNullOrEmpty(cv.PublicId))
            await _cloudinaryService.DeleteAsync(cv.PublicId, cancellationToken);

        // delete from DB
        await _cvRepository.DeleteAsync(cv);
        await _cvRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}