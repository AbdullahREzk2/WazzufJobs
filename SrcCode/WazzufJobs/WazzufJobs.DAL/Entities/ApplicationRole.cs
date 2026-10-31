using Microsoft.AspNetCore.Identity;

namespace WazzufJobs.DAL.Entities;

public class ApplicationRole : IdentityRole
{
    public bool IsDefault { get; set; } = false;
    public bool IsDeleted { get; set; } = false;

    // Phase 02 (S2a): fixed platform roles
    public bool IsSystem { get; set; }
    public string? Description { get; set; }
}