using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

// Phase 02 (S2a): candidate profile and resumes (§1)

public class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.Headline).HasMaxLength(200);
        builder.Property(p => p.Location).HasMaxLength(200);
        builder.Property(p => p.MinSalary).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CareerLevel).HasConversion<byte>();
        builder.Property(p => p.RowVersion).IsRowVersion();
        // preference lists are EF Core primitive collections (JSON columns)
    }
}

public class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.FileName).IsRequired().HasMaxLength(255);
        builder.Property(r => r.StorageKey).IsRequired().HasMaxLength(300);
        builder.Property(r => r.Sha256).IsRequired().HasMaxLength(32).IsFixedLength();
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.StorageKey).IsUnique();
        builder.HasIndex(r => new { r.CandidateId, r.Status });

        // one default resume per candidate
        builder.HasIndex(r => r.CandidateId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1")
            .HasDatabaseName("UX_Resumes_DefaultPerCandidate");

        builder.HasOne(r => r.Candidate)
            .WithMany(u => u.Resumes)
            .HasForeignKey(r => r.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Text)
            .WithOne(t => t.Resume)
            .HasForeignKey<ResumeText>(t => t.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ResumeTextConfiguration : IEntityTypeConfiguration<ResumeText>
{
    public void Configure(EntityTypeBuilder<ResumeText> builder)
    {
        builder.HasKey(t => t.ResumeId);
        builder.Property(t => t.ResumeId).ValueGeneratedNever();
        builder.Property(t => t.ExtractedText).IsRequired().HasColumnType("nvarchar(max)");
    }
}
