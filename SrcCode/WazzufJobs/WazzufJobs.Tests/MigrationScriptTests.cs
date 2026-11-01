using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// The idempotent deployment script generated from the migrations (the artifact a production deployment runs) applies
/// cleanly to an empty database, can be re-run without changes, and yields the same schema as the migration itself
/// (Phase 02, S2a). Uses its own throwaway databases, dropped afterwards.
/// </summary>
public class MigrationScriptTests
{
    private const string Prefix = "WazzufJobs_ScriptTest_";

    private static string ConnectionFor(string database) =>
        new SqlConnectionStringBuilder(SqlServerApiFactory.ServerConnection) { InitialCatalog = database }.ConnectionString;

    private static string IdempotentScript()
    {
        using var context = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseSqlServer("Server=unused;Database=unused").Options);
        return context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
    }

    private static async Task RunScriptAsync(string database, string script)
    {
        await using var connection = new SqlConnection(ConnectionFor(database));
        await connection.OpenAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> SchemaFingerprintAsync(string database)
    {
        await using var connection = new SqlConnection(ConnectionFor(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT STRING_AGG(CAST(x AS nvarchar(max)) COLLATE Latin1_General_BIN2, '|') WITHIN GROUP (ORDER BY x COLLATE Latin1_General_BIN2) FROM (
              SELECT 'T:' + t.name + '.' + c.name + ':' + TYPE_NAME(c.system_type_id) + ':' + CAST(c.max_length AS varchar) + ':' + CAST(c.is_nullable AS varchar)
                FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id
              UNION ALL SELECT 'I:' + OBJECT_NAME(i.object_id) + '.' + i.name + ':' + CAST(i.is_unique AS varchar) + ':' + ISNULL(i.filter_definition, '')
                FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE i.name IS NOT NULL
              UNION ALL SELECT 'F:' + fk.name + ':' + fk.delete_referential_action_desc FROM sys.foreign_keys fk
              UNION ALL SELECT 'R:' + tr.name FROM sys.triggers tr
            ) s(x)
            """, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task CreateDatabaseAsync(string database)
    {
        await using var connection = new SqlConnection(SqlServerApiFactory.ServerConnection);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"CREATE DATABASE [{database}]", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string database)
    {
        if (!database.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop a database that is not a script-test database.");
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(SqlServerApiFactory.ServerConnection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The shared header every deployment script starts with (tools/p02/db/session-options.sql).</summary>
    private static string SessionOptionsHeader()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "p02", "db", "session-options.sql")))
            dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir?.FullName ?? throw new FileNotFoundException("session-options.sql not found"),
            "tools", "p02", "db", "session-options.sql"));
    }

    private static async Task RunInSessionAsync(SqlConnection connection, string script)
    {
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
    }

    // finding F-S2a-1: sqlcmd starts sessions with QUOTED_IDENTIFIER OFF; the header must make the script independent of it
    [SqlServerFact]
    public async Task Script_with_the_session_header_applies_even_when_the_session_starts_with_quoted_identifier_off()
    {
        var database = $"{Prefix}{Guid.NewGuid():N}";
        try
        {
            await CreateDatabaseAsync(database);
            await using var connection = new SqlConnection(ConnectionFor(database));
            await connection.OpenAsync();
            await RunInSessionAsync(connection, "SET QUOTED_IDENTIFIER OFF; SET ANSI_NULLS OFF;");

            await RunInSessionAsync(connection, SessionOptionsHeader() + "\nGO\n" + IdempotentScript());

            await using var check = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
                .UseSqlServer(ConnectionFor(database)).Options);
            Assert.Equal(check.Database.GetMigrations(), await check.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task Without_the_header_the_script_fails_atomically_under_sqlcmd_defaults()
    {
        var database = $"{Prefix}{Guid.NewGuid():N}";
        try
        {
            await CreateDatabaseAsync(database);
            await using var connection = new SqlConnection(ConnectionFor(database));
            await connection.OpenAsync();
            await RunInSessionAsync(connection, "SET QUOTED_IDENTIFIER OFF;");

            var error = await Assert.ThrowsAsync<SqlException>(() => RunInSessionAsync(connection, IdempotentScript()));
            Assert.Equal(1934, error.Number);   // filtered index requires QUOTED_IDENTIFIER ON
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task Header_verification_stops_every_later_batch_when_an_option_is_wrong()
    {
        var database = $"{Prefix}{Guid.NewGuid():N}";
        try
        {
            await CreateDatabaseAsync(database);
            await using var connection = new SqlConnection(ConnectionFor(database));
            await connection.OpenAsync();
            await RunInSessionAsync(connection, "SET QUOTED_IDENTIFIER OFF;");

            // run only the verification part of the header (as if the SET batch had been removed), then a change
            var verification = SessionOptionsHeader();
            verification = verification[verification.IndexOf("-- Verification.", StringComparison.Ordinal)..];
            await Assert.ThrowsAsync<SqlException>(() => RunInSessionAsync(connection, verification));
            await RunInSessionAsync(connection, "CREATE TABLE dbo.MustNotExist (Id int);");

            await RunInSessionAsync(connection, "SET NOEXEC OFF;");
            await using var probe = new SqlCommand("SELECT OBJECT_ID('dbo.MustNotExist')", connection);
            Assert.Equal(DBNull.Value, await probe.ExecuteScalarAsync());
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task Idempotent_script_applies_twice_and_matches_the_migrated_schema()
    {
        var scripted = $"{Prefix}{Guid.NewGuid():N}";
        var migrated = $"{Prefix}{Guid.NewGuid():N}";
        try
        {
            var script = IdempotentScript();
            Assert.Contains("TR_AuditLogs_AppendOnly", script);

            await CreateDatabaseAsync(scripted);
            await RunScriptAsync(scripted, script);
            var afterFirstRun = await SchemaFingerprintAsync(scripted);

            await RunScriptAsync(scripted, script);   // re-run: must be a no-op
            Assert.Equal(afterFirstRun, await SchemaFingerprintAsync(scripted));

            await CreateDatabaseAsync(migrated);
            await using (var context = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
                .UseSqlServer(ConnectionFor(migrated)).Options))
            {
                await context.Database.MigrateAsync();
            }
            Assert.Equal(await SchemaFingerprintAsync(migrated), afterFirstRun);

            await using var check = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
                .UseSqlServer(ConnectionFor(scripted)).Options);
            Assert.Equal(check.Database.GetMigrations(), await check.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await DropDatabaseAsync(scripted);
            await DropDatabaseAsync(migrated);
        }
    }
}
