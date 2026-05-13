using Microsoft.AspNetCore.Identity;

namespace WazzufJobs.DAL.Entities;

public class ApplicationRole : IdentityRole
{
    public bool IsDefault { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
}