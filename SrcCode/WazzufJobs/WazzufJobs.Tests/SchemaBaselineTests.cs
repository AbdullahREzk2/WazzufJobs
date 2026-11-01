using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.EntitiesConfiguration;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Verifies the Baseline_v2 schema built by the migration against the Phase 02 data model and the security
/// invariants it enforces at database level (Phase 02, S2a). Runs on a throwaway SQL Server database.
/// </summary>
public class SchemaBaselineTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    private ApplicationDBContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(factory.ConnectionString).Options);

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map)
    {
        await using var connection = new SqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(map(reader));
        return rows;
    }

    [SqlServerFact]
    public async Task Migration_history_holds_exactly_the_baseline()
    {
        await using var db = NewContext();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.Single(applied);
        Assert.EndsWith("_Baseline_v2", applied[0]);
        Assert.Equal(db.Database.GetMigrations(), applied);
    }

    [SqlServerFact]
    public async Task All_phase_02_tables_exist()
    {
        var tables = await QueryAsync("SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo')", r => r.GetString(0));

        string[] expected =
        [
            "AspNetUsers", "AspNetRoles", "AspNetRoleClaims", "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens",
            "UserSessions", "RefreshTokens", "AuthChallenges", "UserRecoveryCodes",
            "CandidateProfiles", "Resumes", "ResumeTexts", "SavedJobs",
            "Companies", "CompanyMembers",
            "JobCategories", "Jobs", "Applications", "ApplicationStatusHistory",
            "AuditLogs", "PlatformState",
            // transitional until the session-based refresh tokens replace it (S6a)
            "LegacyRefreshTokens"
        ];
        Assert.Empty(expected.Except(tables));

        // S2b: the legacy CV and preference tables are gone (Resumes and CandidateProfiles replace them)
        Assert.DoesNotContain("CVs", tables);
        Assert.DoesNotContain("UserPreferences", tables);
    }

    [SqlServerTheory]
    [InlineData("RefreshTokens", "UX_RefreshTokens_ActivePerSession", "([ConsumedAt] IS NULL AND [RevokedAt] IS NULL)")]
    [InlineData("Resumes", "UX_Resumes_DefaultPerCandidate", "([IsDefault]=(1))")]
    [InlineData("CompanyMembers", "UX_CompanyMembers_ActivePerUser", "([Status]=(0))")]
    [InlineData("Companies", "UX_Companies_SinglePlatform", "([IsPlatform]=(1))")]
    public async Task Filtered_unique_indexes_enforce_the_invariants(string table, string index, string filter)
    {
        var rows = await QueryAsync(
            $"SELECT is_unique, has_filter, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.{table}') AND name = '{index}'",
            r => (Unique: r.GetBoolean(0), HasFilter: r.GetBoolean(1), Filter: r.GetString(2)));

        var row = Assert.Single(rows);
        Assert.True(row.Unique);
        Assert.True(row.HasFilter);
        Assert.Equal(filter, row.Filter);
    }

    [SqlServerTheory]
    [InlineData("RefreshTokens", "TokenHash")]
    [InlineData("AuthChallenges", "TokenHash")]
    [InlineData("Resumes", "StorageKey")]
    [InlineData("Companies", "Slug")]
    [InlineData("Applications", "JobId,CandidateId")]
    [InlineData("UserRecoveryCodes", "UserId,CodeHash")]
    public async Task Unique_constraints_exist(string table, string columns)
    {
        var uniqueIndexColumns = await QueryAsync($"""
            SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('dbo.{table}') AND i.is_unique = 1 AND i.has_filter = 0 AND ic.is_included_column = 0
            GROUP BY i.index_id
            """, r => r.GetString(0));

        Assert.Contains(columns, uniqueIndexColumns);
    }

    [SqlServerTheory]
    [InlineData("Jobs")]
    [InlineData("Applications")]
    [InlineData("Companies")]
    [InlineData("CompanyMembers")]
    [InlineData("CandidateProfiles")]
    [InlineData("Resumes")]
    [InlineData("UserSessions")]
    public async Task Mutable_aggregates_have_a_rowversion(string table)
    {
        var types = await QueryAsync(
            $"SELECT TYPE_NAME(system_type_id) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.{table}') AND name = 'RowVersion'",
            r => r.GetString(0));

        Assert.Equal("timestamp", Assert.Single(types));
    }

    [SqlServerTheory]
    [InlineData("RefreshTokens", "TokenHash")]
    [InlineData("AuthChallenges", "TokenHash")]
    [InlineData("AuthChallenges", "BindingHash")]
    [InlineData("UserRecoveryCodes", "CodeHash")]
    [InlineData("Resumes", "Sha256")]
    [InlineData("UserSessions", "IpHash")]
    [InlineData("AuditLogs", "IpHash")]
    public async Task Hashes_are_fixed_32_byte_binary(string table, string column)
    {
        var columns = await QueryAsync(
            $"SELECT TYPE_NAME(system_type_id), max_length FROM sys.columns WHERE object_id = OBJECT_ID('dbo.{table}') AND name = '{column}'",
            r => (Type: r.GetString(0), Length: r.GetInt16(1)));

        var c = Assert.Single(columns);
        Assert.Equal("binary", c.Type);
        Assert.Equal(32, c.Length);
    }

    [SqlServerTheory]
    // Restrict everywhere except owned rows (architecture §1)
    [InlineData("Applications", "AspNetUsers", "NO_ACTION")]
    [InlineData("Applications", "Resumes", "NO_ACTION")]
    [InlineData("Applications", "Companies", "NO_ACTION")]
    [InlineData("Jobs", "Companies", "NO_ACTION")]
    [InlineData("Resumes", "AspNetUsers", "NO_ACTION")]
    [InlineData("Companies", "AspNetUsers", "NO_ACTION")]
    [InlineData("CompanyMembers", "Companies", "NO_ACTION")]
    [InlineData("ResumeTexts", "Resumes", "CASCADE")]
    [InlineData("UserSessions", "AspNetUsers", "CASCADE")]
    [InlineData("RefreshTokens", "UserSessions", "CASCADE")]
    [InlineData("CandidateProfiles", "AspNetUsers", "CASCADE")]
    // transitional until P04 (DeleteJob still deletes jobs with applications)
    [InlineData("Applications", "Jobs", "CASCADE")]
    public async Task Delete_rules_match_the_data_model(string table, string referenced, string action)
    {
        var actions = await QueryAsync($"""
            SELECT delete_referential_action_desc FROM sys.foreign_keys
            WHERE parent_object_id = OBJECT_ID('dbo.{table}') AND referenced_object_id = OBJECT_ID('dbo.{referenced}')
            """, r => r.GetString(0));

        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.Equal(action, a));
    }

    [SqlServerFact]
    public async Task Audit_log_is_append_only()
    {
        await using var db = NewContext();
        var entry = new AuditLog { Action = "test.append-only", ActorKind = AuditActorKind.System, Outcome = AuditOutcome.Success };
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync();
        Assert.True(entry.Id > 0);

        var update = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE dbo.AuditLogs SET Action = 'tampered' WHERE Id = {0}", entry.Id));
        Assert.Equal(51000, update.Number);

        var delete = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM dbo.AuditLogs WHERE Id = {0}", entry.Id));
        Assert.Equal(51000, delete.Number);

        await using var check = NewContext();
        var stored = await check.AuditLogs.SingleAsync(a => a.Id == entry.Id);
        Assert.Equal("test.append-only", stored.Action);
    }

    [SqlServerFact]
    public async Task At_most_one_usable_refresh_token_per_session()
    {
        await using var db = NewContext();
        var user = TestData.NewUser("session");
        var session = new UserSession { Id = Guid.NewGuid(), User = user, AbsoluteExpiresAt = DateTime.UtcNow.AddDays(1) };
        db.AddRange(user, session, Token(session, 1));
        await db.SaveChangesAsync();

        db.Add(Token(session, 2));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // after the first token is consumed, its successor may be inserted (rotation)
        var first = await db.SessionRefreshTokens.SingleAsync(t => t.SessionId == session.Id);
        first.ConsumedAt = DateTime.UtcNow;
        var successor = Token(session, 3);
        successor.ParentId = first.Id;
        db.Add(successor);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.SessionRefreshTokens.CountAsync(t => t.SessionId == session.Id && t.ConsumedAt == null && t.RevokedAt == null));

        static SessionRefreshToken Token(UserSession s, int _) => new()
        {
            SessionId = s.Id,
            TokenHash = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
    }

    [SqlServerFact]
    public async Task A_user_has_at_most_one_active_company_membership()
    {
        await using var db = NewContext();
        var user = TestData.NewUser("member");
        Company NewCompany() => new() { Id = Guid.NewGuid(), Name = "Co", Slug = $"co-{Guid.NewGuid():N}" };
        var a = NewCompany();
        var b = NewCompany();
        db.AddRange(user, a, b, new CompanyMember { Company = a, User = user, Role = CompanyMemberRole.Owner });
        await db.SaveChangesAsync();

        db.Add(new CompanyMember { CompanyId = b.Id, UserId = user.Id, Role = CompanyMemberRole.Recruiter });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // a removed membership does not count
        db.Add(new CompanyMember { CompanyId = b.Id, UserId = user.Id, Role = CompanyMemberRole.Recruiter, Status = CompanyMemberStatus.Removed });
        await db.SaveChangesAsync();
    }

    [SqlServerFact]
    public async Task One_default_resume_per_candidate_and_a_single_platform_company()
    {
        await using var db = NewContext();
        var candidate = TestData.NewUser("resume");
        Resume NewResume(bool isDefault) => new()
        {
            Candidate = candidate, FileName = "cv.pdf", StorageKey = $"cvs/{Guid.NewGuid():N}", Sha256 = new byte[32], IsDefault = isDefault
        };
        db.AddRange(candidate, NewResume(true), NewResume(false));
        await db.SaveChangesAsync();

        db.Add(new Resume { CandidateId = candidate.Id, FileName = "b.pdf", StorageKey = $"cvs/{Guid.NewGuid():N}", Sha256 = new byte[32], IsDefault = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // the platform company is seeded by Baseline_v2; a second one is rejected
        Assert.Equal(PlatformCompany.Id, (await db.Companies.SingleAsync(c => c.IsPlatform)).Id);
        db.Add(new Company { Id = Guid.NewGuid(), Name = "Platform B", Slug = $"pb-{Guid.NewGuid():N}", IsPlatform = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Platform_state_records_the_schema_baseline()
    {
        await using var db = NewContext();

        var baseline = await db.PlatformState.SingleAsync(s => s.Key == PlatformStateConfiguration.SchemaBaselineKey);

        Assert.Equal("v2", baseline.Value);
    }
}
