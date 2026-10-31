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
