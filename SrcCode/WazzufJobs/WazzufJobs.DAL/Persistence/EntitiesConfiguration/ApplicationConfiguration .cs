using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;
public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.HasKey(a => a.Id);

        // A candidate can only apply to a job once (duplicate apply -> 409)
        builder.Property(a => a.UserId)
            .HasColumnName("CandidateId");

        builder.HasIndex(a => new { a.JobId, a.UserId }).IsUnique();
        builder.HasIndex(a => new { a.CompanyId, a.Status });

        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.AIScore)
            .HasColumnType("real");

        builder.Property(a => a.AIFeedback)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.AppliedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(a => a.User)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // transitional (S2a): cascade kept because DeleteJob still deletes jobs with applications; Restrict from P04,
        // when jobs are closed instead of deleted
        builder.HasOne(a => a.Job)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Company)
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // a resume referenced by an application cannot be deleted
        builder.HasOne(a => a.Resume)
            .WithMany()
            .HasForeignKey(a => a.ResumeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
