namespace WazzufJobs.BLL.Errors;
public record RoleErros
{
    public static Error RoleNotFound =
        new("Role.NotFound", "There's No Role with this Id", StatusCodes.Status404NotFound);

    public static Error RoleAlreadyExist =
        new("Role.RoleAlreadyExist", "Role Already Exist with the same Name ", StatusCodes.Status409Conflict);

    public static Error InvalidPermissions =
        new("Role.InvalidPermissions", "Invalid Permissions ", StatusCodes.Status409Conflict);

    public static Error CreationFailed =
        new("Role.CreationFailed", "Creation Failed ", StatusCodes.Status500InternalServerError);

    public static Error PermissionAssignmentFailed =
        new("Role.PermissionAssignmentFailed", "Permission Assignment Failed ", StatusCodes.Status500InternalServerError);

    public static Error UpdateFailed =
        new("Role.UpdateFailed", "Update Failed ", StatusCodes.Status500InternalServerError);

    public static Error PermissionRemovalFailed =
        new("Role.PermissionRemovalFailed", "Permission Removal Failed", StatusCodes.Status400BadRequest);

}
