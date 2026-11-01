namespace WazzufJobs.BLL.Errors;
public record UserErrors
{
    public static Error InvalidCredentials =
        new("User.InvalidCredentials", "Invalid Email / Password", StatusCodes.Status401Unauthorized);

    public static Error DisabledUser =
        new("User.DisabledUser", "Disabled User plz Contact your Admin", StatusCodes.Status401Unauthorized);

    public static Error InvalidToken =
        new("User.InvalidToken", "The provided token is invalid or has expired.", StatusCodes.Status401Unauthorized);

    public static Error UserNotFound =
        new("User.NotFound", "The specified user does not exist.", StatusCodes.Status404NotFound);

    public static Error RefreshTokenNotFound =
        new("User.RefreshTokenNotFound", "The provided refresh token does not exist.", StatusCodes.Status400BadRequest);

    public static Error DuplicatedEmail =
        new("User.DuplicatedEmail", "Email Already Exists ", StatusCodes.Status409Conflict);

    public static Error EmailNotConfirmed =
        new("User.EmailNotConfirmed", "Email is not Confirmed ", StatusCodes.Status401Unauthorized);

    public static Error DuplicateConfirmation =
        new("User.DuplicateConfirmation", "Email Already Confirmed ", StatusCodes.Status400BadRequest);

    public static Error InvalidCode =
        new("User.InvalidCode", "Invalid Code ", StatusCodes.Status400BadRequest);

    public static Error UserLockedOut =
        new("User.UserLockedOut", "User LockedOut , try again after 5 min ", StatusCodes.Status401Unauthorized);

    public static Error InvalidRoles =
        new("User.InvalidRoles", "Invalid Roles", StatusCodes.Status400BadRequest);

    public static readonly Error ImageUploadFailed =
        new("User.ImageUploadFailed", "Failed to upload image", StatusCodes.Status400BadRequest);

    public static readonly Error GoogleAccountNotLinkable =
        new("User.GoogleAccountNotLinkable", "This email has an unconfirmed account. Confirm your email or reset your password before using Google sign-in.", StatusCodes.Status409Conflict);

    public static readonly Error LastAdministrator =
        new("User.LastAdministrator", "The last active Super Admin cannot be disabled or lose the SuperAdmin role.", StatusCodes.Status400BadRequest);

    /// <summary>Phase 02 (S3): staff roles are never granted or revoked through the Users API.</summary>
    public static readonly Error StaffRolesManagedSeparately =
        new("Users.StaffRolesManagedSeparately", "Staff roles cannot be granted or revoked here.", StatusCodes.Status403Forbidden);

    public static readonly Error RoleAccountTypeMismatch =
        new("User.RoleAccountTypeMismatch", "An account holds at most one role, matching its account type.", StatusCodes.Status400BadRequest);
}

