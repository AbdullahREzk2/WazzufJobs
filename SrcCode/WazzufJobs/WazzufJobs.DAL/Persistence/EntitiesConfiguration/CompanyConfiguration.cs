using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

// Phase 02 (S2a): companies and memberships (§1, §3)

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Website).HasMaxLength(300);
        builder.Property(c => c.Industry).HasMaxLength(100);
        builder.Property(c => c.Size).HasMaxLength(50);
        builder.Property(c => c.Location).HasMaxLength(200);
        builder.Property(c => c.Description).HasColumnType("nvarchar(max)");
        builder.Property(c => c.LogoKey).HasMaxLength(300);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => new { c.Status, c.VerificationStatus });

        // at most one platform company
        builder.HasIndex(c => c.IsPlatform)
            .IsUnique()
            .HasFilter("[IsPlatform] = 1")
            .HasDatabaseName("UX_Companies_SinglePlatform");

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.HasKey(m => new { m.CompanyId, m.UserId });
        builder.Property(m => m.RowVersion).IsRowVersion();

        // D6: one active company membership per account
        builder.HasIndex(m => m.UserId)
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("UX_CompanyMembers_ActivePerUser");

        builder.HasOne(m => m.Company)
            .WithMany(c => c.Members)
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany(u => u.CompanyMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AddedBy)
            .WithMany()
            .HasForeignKey(m => m.AddedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
