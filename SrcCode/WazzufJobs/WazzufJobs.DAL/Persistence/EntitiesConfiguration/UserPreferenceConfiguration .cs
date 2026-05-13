using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;
public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PreferredJobTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<JobType>>(v, (JsonSerializerOptions?)null) ?? new()
            )
            .HasColumnType("nvarchar(200)")
            .Metadata.SetValueComparer(new ValueComparer<List<JobType>>(
               (c1, c2) => c1!.SequenceEqual(c2!),
               c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
               c => c.ToList()));

        builder.Property(p => p.PreferredWorkplaceTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<WorkplaceType>>(v, (JsonSerializerOptions?)null) ?? new()
            )
            .HasColumnType("nvarchar(100)")
            .Metadata.SetValueComparer(new ValueComparer<List<WorkplaceType>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(p => p.InterestedCategoryIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new()
            )
            .HasColumnType("nvarchar(200)")
            .Metadata.SetValueComparer(new ValueComparer<List<int>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));


        builder.Property(p => p.InterestedJobTitles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
            )
            .HasColumnType("nvarchar(500)")
             .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(p => p.MinSalary)
            .HasColumnType("decimal(18,2)");
    }
}
