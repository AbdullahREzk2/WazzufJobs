using Microsoft.AspNetCore.Identity;

namespace WazzufJobs.DAL.Entities;

/// <summary>A fixed platform role (Phase 02, S3). Rows are seeded from <c>PermissionCatalog</c> and never changed.</summary>
public class ApplicationRole : IdentityRole
{
    public bool IsSystem { get; set; }
    public string? Description { get; set; }
}
