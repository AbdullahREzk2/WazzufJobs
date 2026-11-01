namespace WazzufJobs.BLL.Features.Users.Command.UserToggleStatus;
public class ToggleStatusCommandHandler(IUserRepository userRepository, IAuditLog audit, ISessionService sessions) : IRequestHandler<ToggleStatusCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    // the status change and its audit entry are committed together (inside the Super Admin lock when it applies)
    public async Task<Result> Handle(ToggleStatusCommand request, CancellationToken cancellationToken)
    {
        var disabled = false;
        var revokedSessions = 0;
        return await audit.RecordAsync(async () =>
            {
                var result = await ToggleStatusAsync(request, cancellationToken);
                disabled = result.IsSuccess && (await _userrepository.FindByIdAsync(request.userId))?.IsDisabled == true;
                // Phase 02 (S6a): a disabled account's sessions end with the change
                if (disabled)
                    revokedSessions = (await sessions.RevokeAsync(request.userId, null, null, SessionPolicy.RevokedReason.Disabled, cancellationToken)).Count;
                return result;
            },
            r => r.IsSuccess
                ? AuditEvent.Success(disabled ? AuditActions.UserDisabled : AuditActions.UserEnabled).On(AuditTargets.User, request.userId)
                    .With(("revokedSessions", revokedSessions))
                : UserAudit.Denial(r, request.userId),
            cancellationToken);
    }

    private async Task<Result> ToggleStatusAsync(ToggleStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        var superAdmin = PermissionCatalog.SuperAdmin;
        var disablesSuperAdmin = !user.IsDisabled
            && superAdmin.IsCompatibleWith(user.AccountType, user.IsDemo)
            && (await _userrepository.GetRolesAsync(user)).Contains(superAdmin.Name, StringComparer.OrdinalIgnoreCase);

        if (!disablesSuperAdmin)
            return await ToggleAsync(user);

        // never leave the platform without an active Super Admin (C0-5, generalized in S3). The count and the change run
        // under the SuperAdmin role lock, so two Super Admins disabling each other cannot both succeed.
        return await _userrepository.RunExclusiveForRoleAsync(superAdmin, async () =>
            await _userrepository.CountActiveUsersInRoleAsync(superAdmin, cancellationToken) <= 1
                ? Result.Failure(UserErrors.LastAdministrator)
                : await ToggleAsync(user),
            result => result.IsSuccess,
            cancellationToken);
    }

    private async Task<Result> ToggleAsync(AppUser user)
    {
        user.IsDisabled = !user.IsDisabled;
        var result = await _userrepository.UpdateAsync(user);

        if (result.Succeeded)
            return Result.Success();

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }
}
