namespace WazzufJobs.BLL.Errors;
public record RoleErros
{
    public static Error RoleNotFound =
        new("Role.NotFound", "There's No Role with this Id", StatusCodes.Status404NotFound);

    /// <summary>Phase 02 (S3): platform roles and their permissions are fixed in code.</summary>
    public static readonly Error Fixed =
        new("Roles.Fixed", "Platform roles are fixed and cannot be created, changed or disabled.", StatusCodes.Status403Forbidden);
}
