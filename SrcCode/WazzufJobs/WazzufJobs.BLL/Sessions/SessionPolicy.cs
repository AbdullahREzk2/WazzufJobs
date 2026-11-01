using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Sessions;

/// <summary>Session lifetimes and windows (Phase 02, D-SESSION-TTL).</summary>
public static class SessionPolicy
{
    /// <summary>Access tokens never live longer than this, whatever <c>Jwt:ExpiryMinutes</c> says.</summary>
    public const int AccessTokenMaxMinutes = 15;

    /// <summary>A consumed token presented again within this window is a race (409, nothing issued), not a replay.</summary>
    public static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(10);

    /// <summary>How long a session-check result may be reused (P02-5: revocation effective within 30 s).</summary>
    public static readonly TimeSpan CheckCacheWindow = TimeSpan.FromSeconds(30);

    public static (TimeSpan Sliding, TimeSpan Absolute) Lifetime(AccountType accountType) =>
        accountType == AccountType.Staff
            ? (TimeSpan.FromHours(8), TimeSpan.FromHours(24))
            : (TimeSpan.FromDays(14), TimeSpan.FromDays(30));

    public static class Amr
    {
        public const string Password = "pwd";
        public const string External = "ext";
    }

    public static class RevokedReason
    {
        public const string Logout = "logout";
        public const string SignedOutElsewhere = "signed_out_elsewhere";
        public const string PasswordChanged = "password_changed";
        public const string PasswordReset = "password_reset";
        public const string Disabled = "disabled";
        public const string RolesChanged = "roles_changed";
        public const string RefreshReuse = "refresh_reuse";
    }
}
