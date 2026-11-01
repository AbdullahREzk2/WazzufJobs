namespace WazzufJobs.BLL.Features.Users.Command;

/// <summary>Audit entries for refused user changes: only the security rules (staff roles, last Super Admin, role checks).</summary>
internal static class UserAudit
{
    private static readonly HashSet<string> SecurityRefusals =
    [
        UserErrors.StaffRolesManagedSeparately.Code, UserErrors.LastAdministrator.Code,
        UserErrors.RoleAccountTypeMismatch.Code, UserErrors.InvalidRoles.Code
    ];

    public static AuditEvent? Denial(Result result, string? userId) =>
        !result.IsSuccess && SecurityRefusals.Contains(result.Error.Code)
            ? AuditEvent.Denied(AuditActions.UserChangeDenied).On(AuditTargets.User, userId).With(("reason", result.Error.Code))
            : null;
}
