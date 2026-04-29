using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

public class RoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.HasData(
            new ApplicationRole
            {
                Id = AppRoles.Admin.RoleId,
                Name = AppRoles.Admin.Name,
                NormalizedName = AppRoles.Admin.Name.ToUpper(),
                ConcurrencyStamp = AppRoles.Admin.RoleConcurrencyStamp,
                IsDefault = false,
                IsDeleted = false
            },
            new ApplicationRole
            {
                Id = AppRoles.User.RoleId,
                Name = AppRoles.User.Name,
                NormalizedName = AppRoles.User.Name.ToUpper(),
                ConcurrencyStamp = AppRoles.User.RoleConcurrencyStamp,
                IsDefault = true  ,
                IsDeleted = false
            }
        );
    }
}