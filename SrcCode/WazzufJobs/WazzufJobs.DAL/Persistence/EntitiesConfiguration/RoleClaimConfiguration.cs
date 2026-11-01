using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Authorization;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

public class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<string>> builder)
    {
        // one permission claim per (role, permission) of the code catalog, numbered in catalog order (Phase 02, S3)
        var id = 1;
        builder.HasData(PermissionCatalog.Roles
            .SelectMany(role => role.Permissions, (role, permission) => new IdentityRoleClaim<string>
            {
                Id = id++,
                RoleId = role.Id,
                ClaimType = Permissions.Type,
                ClaimValue = permission
            })
            .ToList());
    }
}
