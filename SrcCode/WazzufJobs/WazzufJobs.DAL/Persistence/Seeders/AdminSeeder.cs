using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Persistence.Seeders;

public enum AdminSeedOutcome
{
    SkippedNotConfigured,
    SkippedAlreadyExists,
    Created,
    Failed
}

public static class AdminSeeder
{
    /// <summary>
    /// One-time bootstrap of the first administrator from configuration (AdminBootstrap:Email / AdminBootstrap:Password),
    /// supplied through host environment variables or user-secrets. There is no default password.
    /// An existing account is never modified, so this can never reset a rotated password.
    /// </summary>
    public static async Task<AdminSeedOutcome> SeedAsync(UserManager<AppUser> userManager, string? adminEmail, string? adminPassword)
    {
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            return AdminSeedOutcome.SkippedNotConfigured;

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            return AdminSeedOutcome.SkippedAlreadyExists;

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
        if (!result.Succeeded)
            return AdminSeedOutcome.Failed;

        await userManager.AddToRoleAsync(admin, AppRoles.Admin.Name);
        return AdminSeedOutcome.Created;
    }
}
