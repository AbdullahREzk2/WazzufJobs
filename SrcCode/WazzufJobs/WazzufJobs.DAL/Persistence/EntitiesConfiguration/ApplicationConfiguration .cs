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

        // tinyint (append-only enum numbering); the API still returns the status name
        builder.Property(a => a.Status)
            .HasConversion<byte>();

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

        // (JobId, CompanyId) -> Jobs (Id, CompanyId): the application's company is always its job's company.
        // Cascade is transitional (T4): DeleteJob still deletes jobs with applications; Restrict from P04, when jobs
        // are closed instead of deleted.
        builder.HasOne(a => a.Job)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => new { a.JobId, a.CompanyId })
            .HasPrincipalKey(j => new { j.Id, j.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Company)
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // (ResumeId, CandidateId) -> Resumes (Id, CandidateId): an application can only use the applicant's own resume.
        // A resume referenced by an application cannot be deleted.
        builder.HasOne(a => a.Resume)
            .WithMany()
            .HasForeignKey(a => new { a.ResumeId, a.UserId })
            .HasPrincipalKey(r => new { r.Id, r.CandidateId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
