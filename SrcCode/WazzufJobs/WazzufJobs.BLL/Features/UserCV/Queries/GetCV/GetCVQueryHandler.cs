using WazzufJobs.BLL.Contracts.UserCV;

namespace WazzufJobs.BLL.Features.UserCV.Queries.GetCV;

public class GetCVQueryHandler(ICVRepository cvRepository): IRequestHandler<GetCVQuery, Result<CVResponse>>
{
    private readonly ICVRepository _cvRepository = cvRepository;

    public async Task<Result<CVResponse>> Handle(GetCVQuery request,CancellationToken cancellationToken)
    {
        var cv = await _cvRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (cv is null)
            return Result.Failure<CVResponse>(CVErrors.NotFound);

        return Result.Success(new CVResponse(
            cv.Id,
            cv.Url,
            cv.FileName,
            cv.UploadedAt));
    }
}