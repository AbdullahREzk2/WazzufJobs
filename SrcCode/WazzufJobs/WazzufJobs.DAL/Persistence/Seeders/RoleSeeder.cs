// WazzufJobs.DAL/Persistence/Seeders/RoleSeeder.cs
using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.Seeders;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager)
    {
        var adminExists = await roleManager.FindByIdAsync(AppRoles.Admin.RoleId);
        if (adminExists is null)
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Id = AppRoles.Admin.RoleId,
                Name = AppRoles.Admin.Name,
                NormalizedName = AppRoles.Admin.Name.ToUpper(),
                ConcurrencyStamp = AppRoles.Admin.RoleConcurrencyStamp,
                IsDefault = false,
                IsDeleted = false
            });
        }

        var userExists = await roleManager.FindByIdAsync(AppRoles.User.RoleId);
        if (userExists is null)
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Id = AppRoles.User.RoleId,
                Name = AppRoles.User.Name,
                NormalizedName = AppRoles.User.Name.ToUpper(),
                ConcurrencyStamp = AppRoles.User.RoleConcurrencyStamp,
                IsDefault = true,
                IsDeleted = false
            });
        }
    }
}