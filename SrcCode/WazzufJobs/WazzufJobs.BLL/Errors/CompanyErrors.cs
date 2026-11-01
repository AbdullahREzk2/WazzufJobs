namespace WazzufJobs.BLL.Errors;

/// <summary>Company module errors (Phase 02, S4). A company the caller cannot reach is always "not found" (D-404).</summary>
public record CompanyErrors
{
    public static readonly Error NotFound =
        new("Company.NotFound", "The company does not exist.", StatusCodes.Status404NotFound);

    public static readonly Error Suspended =
        new("Company.Suspended", "The company is suspended.", StatusCodes.Status403Forbidden);

    public static readonly Error PermissionDenied =
        new("Company.PermissionDenied", "Your company role does not allow this action.", StatusCodes.Status403Forbidden);

    public static readonly Error MemberNotFound =
        new("Company.MemberNotFound", "The member does not exist.", StatusCodes.Status404NotFound);

    public static readonly Error OwnersManagedByOwners =
        new("Company.OwnersManagedByOwners", "Only an owner can change or remove owners, or make someone an owner.", StatusCodes.Status403Forbidden);

    public static readonly Error LastOwner =
        new("Company.LastOwner", "The last active owner cannot be removed or lose the Owner role.", StatusCodes.Status400BadRequest);
}
