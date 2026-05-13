using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Description)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(j => j.Location)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Skills)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
            )
            .HasColumnType("nvarchar(1000)")
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
               (c1, c2) => c1!.SequenceEqual(c2!),
               c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
               c => c.ToList()));

        builder.Property(j => j.JobType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.WorkplaceType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.SalaryMin)
            .HasColumnType("decimal(18,2)");

        builder.Property(j => j.SalaryMax)
            .HasColumnType("decimal(18,2)");

        builder.Property(j => j.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(j => j.Category)
            .WithMany(c => c.Jobs)
            .HasForeignKey(j => j.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(j => j.PostedBy)
            .WithMany(u => u.PostedJobs)
            .HasForeignKey(j => j.PostedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for search performance
        builder.HasIndex(j => j.Title);
        builder.HasIndex(j => j.Location);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CategoryId);
    }
}
