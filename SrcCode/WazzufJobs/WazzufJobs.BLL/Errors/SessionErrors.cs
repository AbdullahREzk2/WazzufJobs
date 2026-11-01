namespace WazzufJobs.BLL.Errors;

/// <summary>Session and refresh-token errors (Phase 02, S6a; architecture §5.2).</summary>
public record SessionErrors
{
    /// <summary>Unknown, expired, revoked or replayed token, or an inactive session: never says which.</summary>
    public static readonly Error RefreshInvalid =
        new("Auth.RefreshInvalid", "The session is no longer valid. Please sign in again.", StatusCodes.Status401Unauthorized);

    /// <summary>The token was rotated by a concurrent request moments ago: retry with the successor. Nothing is issued.</summary>
    public static readonly Error RefreshRaced =
        new("Auth.RefreshRaced", "The session was refreshed by another request. Retry shortly.", StatusCodes.Status409Conflict);

    public static readonly Error NotFound =
        new("Session.NotFound", "The session does not exist.", StatusCodes.Status404NotFound);
}
