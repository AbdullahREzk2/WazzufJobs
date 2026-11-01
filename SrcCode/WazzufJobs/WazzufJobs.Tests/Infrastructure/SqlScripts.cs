using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>Result of running a deployment script: every PRINT/error line and whether an error stopped it.</summary>
public sealed record ScriptRun(IReadOnlyList<string> Messages, bool Failed)
{
    public string Output => string.Join(Environment.NewLine, Messages);
}

/// <summary>
/// Runs the reviewed SQL deployment scripts in tools/p02/db the way an operator does with "sqlcmd -b": batch by batch
/// on one session, collecting all messages and stopping at the first error (Phase 02, S2c). Only throwaway databases
/// are ever used (<see cref="ThrowawayDatabase"/>).
/// </summary>
public static class SqlScripts
{
    public const string ExpectedDatabaseLine = "EXEC sys.sp_set_session_context @key = N'wj.ExpectedDatabase', @value = N'';";
    public const string ApplyLine = "EXEC sys.sp_set_session_context @key = N'wj.Apply', @value = 0;";

    public static string Directory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "p02", "db", "session-options.sql")))
                dir = dir.Parent;
            return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("tools/p02/db not found"), "tools", "p02", "db");
        }
    }

    public static string Load(string fileName) => File.ReadAllText(Path.Combine(Directory, fileName));

    /// <summary>Fills in the operator input exactly as an operator would edit it.</summary>
    public static string WithInput(string script, string? expectedDatabase, bool apply = false)
    {
        Assert.Contains(ExpectedDatabaseLine, script);
        script = script.Replace(ExpectedDatabaseLine,
            $"EXEC sys.sp_set_session_context @key = N'wj.ExpectedDatabase', @value = N'{expectedDatabase}';");
        if (apply)
        {
            Assert.Contains(ApplyLine, script);
            script = script.Replace(ApplyLine, "EXEC sys.sp_set_session_context @key = N'wj.Apply', @value = 1;");
        }
        return script;
    }

    public static async Task<ScriptRun> RunAsync(string connectionString, string script)
    {
        var messages = new List<string>();
        var failed = false;
        await using var connection = new SqlConnection(connectionString) { FireInfoMessageEventOnUserErrors = true };
        connection.InfoMessage += (_, e) =>
        {
            foreach (SqlError error in e.Errors)
            {
                messages.Add(error.Message);
                if (error.Class >= 11)
                    failed = true;
            }
        };
        await connection.OpenAsync();

        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 180 };
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                messages.Add(ex.Message);
                failed = true;
            }
            if (failed)
                break;   // sqlcmd -b: stop at the first error
        }
        return new ScriptRun(messages, failed);
    }
}

/// <summary>A uniquely named local database that is always dropped (drop refused for any other name).</summary>
public sealed class ThrowawayDatabase : IAsyncDisposable
{
    private const string Prefix = "WazzufJobs_ScriptTest_";

    public string Name { get; } = $"{Prefix}{Guid.NewGuid():N}";
    public string ConnectionString { get; }

    private ThrowawayDatabase() =>
        ConnectionString = new SqlConnectionStringBuilder(SqlServerApiFactory.ServerConnection) { InitialCatalog = Name }.ConnectionString;

    public static async Task<ThrowawayDatabase> CreateAsync()
    {
        var db = new ThrowawayDatabase();
        await ExecuteOnServerAsync($"CREATE DATABASE [{db.Name}]");
        return db;
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Every table with its row count, every foreign key and trigger: equal fingerprints mean nothing changed.</summary>
    public Task<string> FingerprintAsync() => ScalarAsync<string>("""
        SELECT ISNULL(STRING_AGG(CAST(x AS nvarchar(max)) COLLATE Latin1_General_BIN2, '|') WITHIN GROUP (ORDER BY x COLLATE Latin1_General_BIN2), '') FROM (
          SELECT 'T:' + SCHEMA_NAME(t.schema_id) + '.' + t.name + ':' + CAST((SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) AS varchar(20))
            FROM sys.tables t WHERE t.is_ms_shipped = 0
          UNION ALL SELECT 'F:' + name FROM sys.foreign_keys
          UNION ALL SELECT 'R:' + name FROM sys.triggers
          UNION ALL SELECT 'S:' + name FROM sys.schemas WHERE schema_id BETWEEN 5 AND 16383
        ) s(x)
        """);

    public async Task<IReadOnlyList<string>> TablesAsync()
    {
        var list = await ScalarAsync<string>(
            "SELECT ISNULL(STRING_AGG(CAST(SCHEMA_NAME(schema_id) + '.' + name AS nvarchar(max)), ','), '') FROM sys.tables WHERE is_ms_shipped = 0");
        return list.Split(',', StringSplitOptions.RemoveEmptyEntries).Order().ToList();
    }

    private static async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new SqlConnection(SqlServerApiFactory.ServerConnection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!Name.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop a database that is not a throwaway script-test database.");
        SqlConnection.ClearAllPools();
        await ExecuteOnServerAsync(
            $"IF DB_ID('{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END");
    }
}

/// <summary>Builds databases in the states the scripts expect: a populated P01 database and a Baseline_v2 database.</summary>
public static class ScriptDatabases
{
    /// <summary>
    /// P01 schema (91_p01_schema.sql), the real Hangfire schema, and synthetic rows in every P01 table: the shape of the
    /// current production database.
    /// </summary>
    public static async Task BuildP01Async(ThrowawayDatabase db)
    {
        var run = await SqlScripts.RunAsync(db.ConnectionString, SqlScripts.Load("91_p01_schema.sql"));
        Assert.False(run.Failed, run.Output);
        await InstallHangfireAsync(db);
        await db.ExecuteAsync("""
            INSERT INTO dbo.AspNetUsers (Id, FirstName, LastName, IsProfileComplete, IsDisabled, ExperienceYears, CareerLevel, ShowSalary, CreatedAt,
                EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, UserName, NormalizedUserName, Email, NormalizedEmail)
            VALUES ('u-1', 'Synthetic', 'One', 1, 0, 3, 'MidLevel', 0, SYSUTCDATETIME(), 1, 0, 0, 1, 0, 'one@example.test', 'ONE@EXAMPLE.TEST', 'one@example.test', 'ONE@EXAMPLE.TEST'),
                   ('u-2', 'Synthetic', 'Two', 0, 0, 0, 'Student', 0, SYSUTCDATETIME(), 1, 0, 0, 1, 0, 'two@example.test', 'TWO@EXAMPLE.TEST', 'two@example.test', 'TWO@EXAMPLE.TEST');
            INSERT INTO dbo.AspNetUserRoles (UserId, RoleId) SELECT 'u-1', Id FROM dbo.AspNetRoles WHERE Name = 'User';
            INSERT INTO dbo.Jobs (Title, Description, Location, Skills, JobType, WorkplaceType, Status, CategoryId, PostedById, CreatedAt)
            VALUES ('Synthetic job', 'd', 'Cairo', '["C#"]', 'FullTime', 'Remote', 'Active', 1, 'u-2', SYSUTCDATETIME());
            INSERT INTO dbo.Applications (UserId, JobId, Status, IsAIScored, AppliedAt) SELECT 'u-1', Id, 'Pending', 0, SYSUTCDATETIME() FROM dbo.Jobs;
            INSERT INTO dbo.CVs (UserId, Url, PublicId, FileName, UploadedAt) VALUES ('u-1', 'https://example.test/raw/upload/x', 'wazzuf-jobs/cvs/x', 'cv.pdf', SYSUTCDATETIME());
            INSERT INTO dbo.RefreshTokens (Token, ExpiresOn, CreatedOn, UserId) VALUES ('synthetic', DATEADD(day, 1, SYSUTCDATETIME()), SYSUTCDATETIME(), 'u-1');
            INSERT INTO dbo.SavedJobs (UserId, JobId, SavedAt) SELECT 'u-1', Id, SYSUTCDATETIME() FROM dbo.Jobs;
            INSERT INTO dbo.UserPreferences (UserId, PreferredJobTypes, PreferredWorkplaceTypes, InterestedCategoryIds, InterestedJobTitles) VALUES ('u-1', '[0]', '[1]', '[1]', '[]');
            INSERT INTO HangFire.Counter ([Key], [Value]) VALUES ('synthetic', 1);
            """);
    }

    /// <summary>Baseline_v2 applied with the reviewed scripts (02 + 03), as after a successful reset.</summary>
    public static async Task BuildBaselineV2Async(ThrowawayDatabase db, bool withHangfire = true)
    {
        var baseline = await SqlScripts.RunAsync(db.ConnectionString, SqlScripts.Load("02_baseline_v2.sql"));
        Assert.False(baseline.Failed, baseline.Output);
        var reference = await SqlScripts.RunAsync(db.ConnectionString, SqlScripts.WithInput(SqlScripts.Load("03_reference_data.sql"), db.Name, apply: true));
        Assert.False(reference.Failed, reference.Output);
        if (withHangfire)
            await InstallHangfireAsync(db);
    }

    public static async Task InstallHangfireAsync(ThrowawayDatabase db)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        Hangfire.SqlServer.SqlServerObjectsInstaller.Install(connection);
    }
}
