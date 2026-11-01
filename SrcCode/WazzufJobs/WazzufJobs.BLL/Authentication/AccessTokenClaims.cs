namespace WazzufJobs.BLL.Authentication;

/// <summary>Custom access-token claim names.</summary>
public static class AccessTokenClaims
{
    /// <summary>Platform roles plus legacy aliases, for the frontend only. Never used for authorization.</summary>
    public const string Roles = "roles";
    public const string AccountType = "acct";
    public const string Permissions = WazzufJobs.DAL.Authorization.Permissions.Type;

    /// <summary>
    /// Role claim type of the validated principal. No token carries it, so <c>IsInRole</c> and role-based
    /// <c>[Authorize]</c> can never succeed: legacy aliases in <see cref="Roles"/> are never authorization grants.
    /// </summary>
    public const string NoRoleAuthorization = "wj:no-role-authorization";
}
