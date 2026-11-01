namespace WazzufJobs.BLL.Features.Users.Command.UpdateUser;
public class UpdateUserCommandHandler(IUserRepository userRepository, IAuditLog audit) : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    // the change and its audit entry are committed together; one entry per request (user.roles.changed when the roles
    // change, user.updated otherwise); refused changes are audited as denied
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var change = new Change();
        return await audit.RecordAsync(() => UpdateAsync(request, change, cancellationToken),
            r => r.IsSuccess
                ? AuditEvent.Success(change.Added.Count + change.Removed.Count > 0 ? AuditActions.UserRolesChanged : AuditActions.UserUpdated)
                    .On(AuditTargets.User, request.userId)
                    .With(("emailChanged", change.EmailChanged), ("rolesAdded", change.Added), ("rolesRemoved", change.Removed))
                : UserAudit.Denial(r, request.userId),
            cancellationToken);
    }

    private sealed class Change
    {
        public bool EmailChanged;
        public List<string> Added = [];
        public List<string> Removed = [];
    }

    private async Task<Result> UpdateAsync(UpdateUserCommand request, Change change, CancellationToken cancellationToken)
    {

        if (await _userrepository.EmailExistsForOtherUserAsync(request.userRequest.Email, request.userId, cancellationToken))
            return Result.Failure(UserErrors.DuplicatedEmail);

        // Phase 02 (S3): only platform roles from the catalog; legacy aliases ("Admin", "User") are not roles
        var requestedRoles = request.userRequest.Roles ?? [];
        if (requestedRoles.Any(r => PermissionCatalog.Find(r) is null))
            return Result.Failure(UserErrors.InvalidRoles);

        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        var currentRoles = await _userrepository.GetRolesAsync(user);

        // never leave the platform without an active Super Admin (C0-5, generalized in S3)
        var superAdmin = PermissionCatalog.SuperAdmin;
        var isActiveSuperAdmin = !user.IsDisabled
            && currentRoles.Contains(superAdmin.Name, StringComparer.OrdinalIgnoreCase)
            && superAdmin.IsCompatibleWith(user.AccountType, user.IsDemo);
        if (isActiveSuperAdmin
            && !requestedRoles.Contains(superAdmin.Name, StringComparer.OrdinalIgnoreCase)
            && await _userrepository.CountActiveUsersInRoleAsync(superAdmin, cancellationToken) <= 1)
            return Result.Failure(UserErrors.LastAdministrator);

        // staff roles are never granted or revoked through the Users API (D-STAFF-ROLES); an unchanged set is accepted,
        // so the admin "Edit user" form (current roles pre-selected) still updates names and email
        if (!StaffRoles(currentRoles).SetEquals(StaffRoles(requestedRoles)))
            return Result.Failure(UserErrors.StaffRolesManagedSeparately);

        if (PermissionCatalog.ValidateAssignment(user.AccountType, user.IsDemo, requestedRoles) != RoleAssignmentError.None)
            return Result.Failure(UserErrors.RoleAccountTypeMismatch);

        var targetRoles = requestedRoles.Select(r => PermissionCatalog.Find(r)!.Name).Distinct().ToList();
        change.EmailChanged = !string.Equals(user.Email, request.userRequest.Email, StringComparison.OrdinalIgnoreCase);
        change.Added = targetRoles.Where(r => !currentRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        change.Removed = currentRoles.Where(r => !targetRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

        user = request.userRequest.Adapt(user);
        user.UserName = request.userRequest.Email;
        user.NormalizedEmail = request.userRequest.Email.ToUpper();

        var result = await _userrepository.UpdateAsync(user);
        if (result.Succeeded)
        {
            var roles = requestedRoles.Select(r => PermissionCatalog.Find(r)!.Name);
            var rolesResult = await _userrepository.ReplaceUserRolesAsync(user, roles, cancellationToken);
            if (!rolesResult.Succeeded)
            {
                var rolesError = rolesResult.Errors.First();
                return Result.Failure(new Error(rolesError.Code, rolesError.Description, StatusCodes.Status400BadRequest));
            }

            return Result.Success();
        }

        var error = result.Errors.First();
        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }

    private static HashSet<string> StaffRoles(IEnumerable<string> roles) =>
        roles.Where(PermissionCatalog.IsStaffRole).Select(r => PermissionCatalog.Find(r)!.Name).ToHashSet();
}
