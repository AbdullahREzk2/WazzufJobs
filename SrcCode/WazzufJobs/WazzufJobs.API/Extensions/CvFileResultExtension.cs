using WazzufJobs.BLL.Contracts.UserCV;

namespace WazzufJobs.API.Extensions;

public static class CvFileResultExtension
{
    /// <summary>Streams a CV as a download that browsers must not cache, sniff, or render with script access.</summary>
    public static IActionResult ToCvFileResult(this ControllerBase controller, Result<CvFileResponse> result)
    {
        if (!result.IsSuccess)
            return result.ToProblem();

        var headers = controller.Response.Headers;
        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "sandbox; default-src 'none'";

        return controller.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
