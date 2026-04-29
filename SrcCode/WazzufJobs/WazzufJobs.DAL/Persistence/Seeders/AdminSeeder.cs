using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Persistence.Seeders;

public static class AdminSeeder
{
    public static async Task SeedAsync(UserManager<AppUser> userManager)
    {
        const string adminEmail = "admin@wazzuf.com";
        const string adminPassword = "Admin@123456";

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new AppUser
        {
            FirstName = "Wazzuf",
            LastName = "Admin",
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            IsProfileComplete = true,
            CareerLevel = CareerLevel.Executive,
            ExperienceYears = 10,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, adminPassword);

        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}