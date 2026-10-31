using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WazzufJobs.DAL.Entities;

namespace WazzufJobs.DAL.Persistence.EntitiesConfiguration;

// Phase 02 (S2a): sessions, refresh tokens, authentication challenges and recovery codes (§5, §6)

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.RevokedReason).HasMaxLength(50);
        builder.Property(s => s.Amr).IsRequired().HasMaxLength(16);
        builder.Property(s => s.UserAgent).HasMaxLength(200);
        builder.Property(s => s.IpHash).HasMaxLength(32).IsFixedLength();
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => new { s.UserId, s.RevokedAt });

        // sessions are owned rows of the user
        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SessionRefreshTokenConfiguration : IEntityTypeConfiguration<SessionRefreshToken>
{
    public void Configure(EntityTypeBuilder<SessionRefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(32).IsFixedLength();

        builder.HasIndex(t => t.TokenHash).IsUnique();

        // at most one usable (not consumed, not revoked) refresh token per session: the database rejects a second one
        builder.HasIndex(t => t.SessionId)
            .IsUnique()
            .HasFilter("[ConsumedAt] IS NULL AND [RevokedAt] IS NULL")
            .HasDatabaseName("UX_RefreshTokens_ActivePerSession");

        builder.HasOne(t => t.Session)
            .WithMany(s => s.RefreshTokens)
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Parent)
            .WithMany()
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuthChallengeConfiguration : IEntityTypeConfiguration<AuthChallenge>
{
    public void Configure(EntityTypeBuilder<AuthChallenge> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.TokenHash).IsRequired().HasMaxLength(32).IsFixedLength();
        builder.Property(c => c.BindingHash).HasMaxLength(32).IsFixedLength();

        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => new { c.UserId, c.Purpose });
        builder.HasIndex(c => c.ExpiresAt);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Session)
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserRecoveryCodeConfiguration : IEntityTypeConfiguration<UserRecoveryCode>
{
    public void Configure(EntityTypeBuilder<UserRecoveryCode> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodeHash).IsRequired().HasMaxLength(32).IsFixedLength();

        builder.HasIndex(c => new { c.UserId, c.CodeHash }).IsUnique();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
