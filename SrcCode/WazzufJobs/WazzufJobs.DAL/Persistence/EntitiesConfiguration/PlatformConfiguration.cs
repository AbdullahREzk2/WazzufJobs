using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

// Phase 02 (S2a): application status history, audit log and platform state (§1, §7)

public class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("ApplicationStatusHistory");
        builder.HasKey(h => h.Id);
        // same storage as Applications.Status
        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.HasIndex(h => new { h.ApplicationId, h.ChangedAt });

        builder.HasOne(h => h.Application)
            .WithMany(a => a.StatusHistory)
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>Created by the Baseline_v2 migration; rejects UPDATE and DELETE (append-only).</summary>
    public const string AppendOnlyTrigger = "TR_AuditLogs_AppendOnly";

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        // declaring the trigger makes EF avoid OUTPUT clauses that SQL Server forbids on tables with triggers
        builder.ToTable(t => t.HasTrigger(AppendOnlyTrigger));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.OccurredAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(a => a.ActorUserId).HasMaxLength(450);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.TargetType).HasMaxLength(100);
        builder.Property(a => a.TargetId).HasMaxLength(100);
        builder.Property(a => a.IpHash).HasMaxLength(32).IsFixedLength();
        builder.Property(a => a.UserAgent).HasMaxLength(200);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.Property(a => a.Details).HasMaxLength(4000);

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.ActorUserId, a.OccurredAt });
        builder.HasIndex(a => new { a.Action, a.OccurredAt });
        builder.HasIndex(a => new { a.CompanyId, a.OccurredAt });
    }
}

public class PlatformStateConfiguration : IEntityTypeConfiguration<PlatformState>
{
    public const string SchemaBaselineKey = "SchemaBaseline";

    public void Configure(EntityTypeBuilder<PlatformState> builder)
    {
        builder.ToTable("PlatformState");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(100);
        builder.Property(s => s.Value).HasMaxLength(400);

        builder.HasData(new PlatformState
        {
            Key = SchemaBaselineKey,
            Value = "v2",
            UpdatedAt = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
