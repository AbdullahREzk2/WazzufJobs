namespace WazzufJobs.BLL.Audit;

/// <summary>Every audit action name (Phase 02, S5). Names are stable: reports and alerts depend on them.</summary>
public static class AuditActions
{
    // authentication (written separately from domain changes)
    public const string LoginSucceeded = "auth.login.succeeded";
    public const string LoginFailed = "auth.login.failed";            // existing accounts only; unknown emails are not audited
    public const string LoginLockedOut = "auth.login.locked_out";
    public const string RefreshDenied = "auth.refresh.denied";
    public const string Logout = "auth.logout";
    public const string GoogleSignIn = "auth.google.succeeded";
    public const string PasswordResetRequested = "auth.password_reset.requested";
    public const string PasswordResetSucceeded = "auth.password_reset.succeeded";
    public const string PasswordResetFailed = "auth.password_reset.failed";

    // accounts
    public const string UserRegistered = "user.registered";
    public const string EmailConfirmed = "user.email_confirmed";
    public const string PasswordChanged = "account.password_changed";
    public const string PasswordChangeFailed = "account.password_change_failed";

    // user administration
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserRolesChanged = "user.roles.changed";
    public const string UserDisabled = "user.disabled";
    public const string UserEnabled = "user.enabled";
    public const string UserUnlocked = "user.unlocked";
    public const string UserChangeDenied = "user.change.denied";
    public const string RolesChangeDenied = "roles.change.denied";

    // authorization
    public const string AuthorizationDenied = "authz.denied";

    // companies
    public const string CompanyAccessDenied = "company.access.denied";
    public const string CompanyMemberRoleChanged = "company.member.role_changed";
    public const string CompanyMemberRemoved = "company.member.removed";
    public const string CompanyMemberChangeDenied = "company.member.change_denied";

    // platform administration
    public const string JobCreated = "job.created";
    public const string JobUpdated = "job.updated";
    public const string JobDeleted = "job.deleted";
    public const string JobStatusChanged = "job.status_changed";
    public const string CategoryCreated = "category.created";
    public const string CategoryUpdated = "category.updated";
    public const string CategoryDeleted = "category.deleted";
    public const string ApplicationStatusChanged = "application.status_changed";
    public const string ApplicationCvAccessed = "applications.cv.accessed";

    public static IReadOnlyList<string> All { get; } = typeof(AuditActions)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();
}
