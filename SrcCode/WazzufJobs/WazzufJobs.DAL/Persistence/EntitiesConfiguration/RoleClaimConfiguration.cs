using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

public class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<string>> builder)
    {
        var allPermissions = Permissions.GetAllPermissions();
        var userPermissions = Permissions.GetUserPermissions();

        var claims = new List<IdentityRoleClaim<string>>();
        int id = 1;

        // Admin gets every permission
        foreach (var permission in allPermissions)
        {
            claims.Add(new IdentityRoleClaim<string>
            {
                Id = id++,
                ClaimType = Permissions.Type,
                ClaimValue = permission,
                RoleId = AppRoles.Admin.RoleId
            });
        }

        // User gets a subset
        foreach (var permission in userPermissions)
        {
            claims.Add(new IdentityRoleClaim<string>
            {
                Id = id++,
                ClaimType = Permissions.Type,
                ClaimValue = permission,
                RoleId = AppRoles.User.RoleId
            });
        }

        builder.HasData(claims);
    }
}