namespace WazzufJobs.BLL.Authentication;
public interface IJwtProvider
{
    /// <summary>Access token for the account with exactly the access resolved by <c>PermissionCatalog.Resolve</c>.</summary>
    (string token, int expireIn) GenerateToken(AppUser user, AccessGrant grant);
    string? validateToken(string token);
}
