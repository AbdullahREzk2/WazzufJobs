using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.Seeders;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync(AppRoles.Admin.Name))
            await roleManager.CreateAsync(new ApplicationRole
            {
                Id = AppRoles.Admin.RoleId,
                Name = AppRoles.Admin.Name,
                NormalizedName = AppRoles.Admin.Name.ToUpper(),
                ConcurrencyStamp = AppRoles.Admin.RoleConcurrencyStamp,
                IsDefault = false,
                IsDeleted = false
            });

        if (!await roleManager.RoleExistsAsync(AppRoles.User.Name))
            await roleManager.CreateAsync(new ApplicationRole
            {
                Id = AppRoles.User.RoleId,
                Name = AppRoles.User.Name,
                NormalizedName = AppRoles.User.Name.ToUpper(),
                ConcurrencyStamp = AppRoles.User.RoleConcurrencyStamp,
                IsDefault = true,
                IsDeleted= false

            });
    }
}