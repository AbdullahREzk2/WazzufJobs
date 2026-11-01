using WazzufJobs.Shared;

namespace WazzufJobs.API.Setting;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpcontextaccessor;
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpcontextaccessor = httpContextAccessor;
    }

    public string? UserId =>
         _httpcontextaccessor.HttpContext?.User.GetUserId();

    public Guid? SessionId =>
        Guid.TryParse(_httpcontextaccessor.HttpContext?.User.GetSessionId(), out var id) ? id : null;

}
