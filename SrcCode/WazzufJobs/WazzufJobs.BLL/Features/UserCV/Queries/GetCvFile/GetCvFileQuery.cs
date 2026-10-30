using WazzufJobs.BLL.Contracts.UserCV;

namespace WazzufJobs.BLL.Features.UserCV.Queries.GetCvFile;

/// <summary>Returns the CV file of the given user. Callers must authorize access before sending this query.</summary>
public record GetCvFileQuery(string UserId) : IRequest<Result<CvFileResponse>>;

/// <summary>Returns the CV file attached to an application (admin review).</summary>
public record GetApplicationCvFileQuery(int ApplicationId) : IRequest<Result<CvFileResponse>>;
