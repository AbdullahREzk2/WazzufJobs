using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;
public class JobCategoryConfiguration : IEntityTypeConfiguration<JobCategory>
{
    public void Configure(EntityTypeBuilder<JobCategory> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.IconUrl)
            .HasMaxLength(500);

        builder.Property(c => c.IconPublicId)
            .HasMaxLength(200);

        builder.HasData(
            new JobCategory { Id = 1, Name = "Software & IT", Slug = "software-it" },
            new JobCategory { Id = 2, Name = "Banking & Finance", Slug = "banking-finance" },
            new JobCategory { Id = 3, Name = "Marketing", Slug = "marketing" },
            new JobCategory { Id = 4, Name = "Healthcare", Slug = "healthcare" },
            new JobCategory { Id = 5, Name = "Engineering", Slug = "engineering" },
            new JobCategory { Id = 6, Name = "Education", Slug = "education" },
            new JobCategory { Id = 7, Name = "Sales", Slug = "sales" },
            new JobCategory { Id = 8, Name = "Design & Creative", Slug = "design-creative" }
        );
    }
}