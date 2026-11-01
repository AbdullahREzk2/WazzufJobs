namespace WazzufJobs.Shared;
public interface ICurrentUserService
{
    string? UserId { get; }

    /// <summary>The access token's session id (<c>sid</c>), if any (Phase 02, S6a).</summary>
    Guid? SessionId => null;

}
