using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.Seeders;

public static class RoleSeeder
{
    /// <summary>Creates any missing fixed platform role. Existing roles are never changed.</summary>
    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in PermissionCatalog.Roles)
        {
            if (await roleManager.FindByIdAsync(role.Id) is not null)
                continue;

            await roleManager.CreateAsync(new ApplicationRole
            {
                Id = role.Id,
                Name = role.Name,
                NormalizedName = role.Name.ToUpperInvariant(),
                ConcurrencyStamp = role.ConcurrencyStamp,
                IsSystem = true,
                Description = role.Description
            });
        }
    }
}
