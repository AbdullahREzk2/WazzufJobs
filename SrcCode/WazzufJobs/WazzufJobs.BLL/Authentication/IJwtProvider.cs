namespace WazzufJobs.BLL.Authentication;
public interface IJwtProvider
{
    /// <summary>
    /// Access token of a session for the account with exactly the access resolved by <c>PermissionCatalog.Resolve</c>.
    /// Lifetime at most 15 minutes (Phase 02, S6a); every token names its session (<c>sid</c>), which the API checks.
    /// </summary>
    (string token, int expireIn) GenerateToken(AppUser user, AccessGrant grant, Guid sessionId, string amr);
    string? validateToken(string token);
}
