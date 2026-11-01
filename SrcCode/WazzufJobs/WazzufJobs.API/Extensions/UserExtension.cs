using System.Security.Claims;

namespace WazzufJobs.API.Extensions;

public static class UserExtension
{
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(claimType: ClaimTypes.NameIdentifier);

    /// <summary>The <c>sid</c> claim (mapped to <see cref="ClaimTypes.Sid"/> by the default inbound claim map).</summary>
    public static string? GetSessionId(this ClaimsPrincipal user)
        => user.FindFirstValue("sid") ?? user.FindFirstValue(ClaimTypes.Sid);

}
