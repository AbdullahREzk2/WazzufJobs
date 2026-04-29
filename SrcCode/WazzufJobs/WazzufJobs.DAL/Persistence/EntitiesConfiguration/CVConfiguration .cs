using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;
public class CVConfiguration : IEntityTypeConfiguration<CV>
{
    public void Configure(EntityTypeBuilder<CV> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.PublicId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.UploadedAt)
            .HasDefaultValueSql("GETUTCDATE()");
    }
}
