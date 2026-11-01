using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

public class RoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(r => r.Description).HasMaxLength(256);

        // the fixed platform roles, from the code catalog (Phase 02, S3)
        builder.HasData(PermissionCatalog.Roles.Select(r => new ApplicationRole
        {
            Id = r.Id,
            Name = r.Name,
            NormalizedName = r.Name.ToUpperInvariant(),
            ConcurrencyStamp = r.ConcurrencyStamp,
            IsSystem = true,
            Description = r.Description
        }));
    }
}
