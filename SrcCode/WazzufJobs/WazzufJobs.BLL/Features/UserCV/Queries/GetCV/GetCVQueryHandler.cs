using WazzufJobs.BLL.Contracts.UserCV;

namespace WazzufJobs.BLL.Features.UserCV.Queries.GetCV;

public class GetCVQueryHandler(IResumeRepository resumeRepository): IRequestHandler<GetCVQuery, Result<CVResponse>>
{
    private readonly IResumeRepository _resumeRepository = resumeRepository;

    public async Task<Result<CVResponse>> Handle(GetCVQuery request,CancellationToken cancellationToken)
    {
        var resume = await _resumeRepository.GetCurrentAsync(request.UserId, includeText: false, cancellationToken);

        if (resume is null)
            return Result.Failure<CVResponse>(CVErrors.NotFound);

        return Result.Success(new CVResponse(
            resume.Id,
            resume.FileName,
            resume.UploadedAt));
    }
}
