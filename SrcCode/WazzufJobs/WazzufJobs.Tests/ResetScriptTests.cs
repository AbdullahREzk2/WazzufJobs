using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Reviewed database deployment scripts (Phase 02, S2c): reset guards, allowlist and atomicity, Baseline_v2 and
/// reference data, verification, rollback to an empty P01 schema, and repeatability. Throwaway databases only.
/// </summary>
public class ResetScriptTests
{
    private const string Reset = "01_reset_drop_application_schema.sql";
    private const string Baseline = "02_baseline_v2.sql";
    private const string ReferenceData = "03_reference_data.sql";
    private const string Verify = "04_verify_schema.sql";
    private const string RollbackDrop = "90_rollback_drop_baseline_v2.sql";
    private const string P01Schema = "91_p01_schema.sql";

    private static readonly string[] P01Migrations = ["20260429160411_initailCreate", "20260517113104_AddCVExtractedText"];

    private static Task<ScriptRun> Run(ThrowawayDatabase db, string file, string? expected = null, bool apply = false, bool input = true) =>
        SqlScripts.RunAsync(db.ConnectionString, input ? SqlScripts.WithInput(SqlScripts.Load(file), expected ?? db.Name, apply) : SqlScripts.Load(file));

    private static async Task<ThrowawayDatabase> P01Async()
    {
        var db = await ThrowawayDatabase.CreateAsync();
        await ScriptDatabases.BuildP01Async(db);
        return db;
    }

    // ---------------------------------------------------------------- reset guards: every refusal changes nothing

    [SqlServerFact]
    public async Task Reset_defaults_to_a_dry_run_that_reports_and_changes_nothing()
    {
        await using var db = await P01Async();
        var before = await db.FingerprintAsync();

        var run = await Run(db, Reset);

        Assert.False(run.Failed, run.Output);
        Assert.Contains("mode DRY RUN", run.Output);
        Assert.Contains("DRY RUN complete: no change was made", run.Output);
        Assert.Contains("[HangFire].[Counter]", run.Output);
        Assert.Matches(@"DROP\s+\[dbo\]\.\[AspNetUsers\]\s+rows=2", run.Output);
        Assert.Equal(before, await db.FingerprintAsync());
    }

    [SqlServerTheory]
    [InlineData("")]                             // not set
    [InlineData("WazzufJobsDB")]                 // another database (e.g. the development database)
    [InlineData("db_a1b2c3_wazzufjobs")]         // e.g. a production-like name
    public async Task Reset_refuses_unless_the_exact_database_name_is_given(string expected)
    {
        await using var db = await P01Async();
        var before = await db.FingerprintAsync();

        var run = await Run(db, Reset, expected, apply: true);

        Assert.True(run.Failed);
        Assert.Contains("STOPPED", run.Output);
        Assert.Equal(before, await db.FingerprintAsync());
    }

    [SqlServerTheory]
    [InlineData("INSERT INTO dbo.__EFMigrationsHistory VALUES ('20261101054955_Baseline_v2', '10.0.14')", "not P01 migrations")]
    [InlineData("INSERT INTO dbo.__EFMigrationsHistory VALUES ('20990101000000_Unknown', '10.0.14')", "not P01 migrations")]
    [InlineData("DELETE FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20260429160411_initailCreate'", "base migration")]
    [InlineData("DROP TABLE dbo.__EFMigrationsHistory", "not a P01 WazzufJobs database")]
    public async Task Reset_refuses_an_unexpected_migration_history(string tamper, string reason)
    {
        await using var db = await P01Async();
        await db.ExecuteAsync(tamper);
        var before = await db.FingerprintAsync();

        var run = await Run(db, Reset, apply: true);

        Assert.True(run.Failed);
        Assert.Contains(reason, run.Output);
        Assert.Equal(before, await db.FingerprintAsync());
    }

    [SqlServerFact]
    public async Task Reset_accepts_a_P01_history_without_the_second_migration()
    {
        // the development database has only the first P01 migration recorded; production may too
        await using var db = await P01Async();
        await db.ExecuteAsync("DELETE FROM dbo.__EFMigrationsHistory WHERE MigrationId = '20260517113104_AddCVExtractedText'");

        var run = await Run(db, Reset);

        Assert.False(run.Failed, run.Output);
    }

    [SqlServerTheory]
    [InlineData("CREATE TABLE dbo.Zz_Ref (Id int PRIMARY KEY, JobId int REFERENCES dbo.Jobs(Id))", "depend on a table to be dropped")]
    [InlineData("EXEC('CREATE VIEW dbo.Zz_Bound WITH SCHEMABINDING AS SELECT Id FROM dbo.Jobs')", "depend on a table to be dropped")]
    [InlineData("CREATE TABLE HangFire.Zz_Extra (Id int)", "unknown table in HangFire schema")]
    [InlineData("CREATE TABLE dbo.Companies (Id int)", "collides with a Baseline_v2 table")]
    public async Task Reset_stops_on_unexpected_objects_that_it_cannot_handle_safely(string setup, string reason)
    {
        await using var db = await P01Async();
        await db.ExecuteAsync(setup);
        var before = await db.FingerprintAsync();

        var run = await Run(db, Reset, apply: true);

        Assert.True(run.Failed);
        Assert.Contains(reason, run.Output);
        Assert.Equal(before, await db.FingerprintAsync());
    }

    // ---------------------------------------------------------------- reset apply

    [SqlServerFact]
    public async Task Reset_drops_only_allowlisted_tables_and_preserves_everything_else()
    {
        await using var db = await P01Async();
        await db.ExecuteAsync("""
            CREATE TABLE dbo.Zz_Unrelated (Id int PRIMARY KEY, Note nvarchar(50)); INSERT INTO dbo.Zz_Unrelated VALUES (1, 'keep me');
            EXEC('CREATE SCHEMA ops');
            CREATE TABLE ops.Notes (Id int); INSERT INTO ops.Notes VALUES (7);
            """);

        var run = await Run(db, Reset, apply: true);

        Assert.False(run.Failed, run.Output);
        Assert.Contains("RESET APPLIED: 26 table(s) dropped; 2 preserved", run.Output);
        Assert.Contains("PRESERVE [dbo].[Zz_Unrelated]", Regex.Replace(run.Output, @"\s+", " "));
        Assert.Equal(["dbo.Zz_Unrelated", "ops.Notes"], await db.TablesAsync());
        Assert.Equal("keep me", await db.ScalarAsync<string>("SELECT Note FROM dbo.Zz_Unrelated"));
        Assert.Equal(7, await db.ScalarAsync<int>("SELECT Id FROM ops.Notes"));
        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.schemas WHERE name = 'HangFire'"));
    }

    [SqlServerFact]
    public async Task A_failure_during_the_reset_rolls_everything_back()
    {
        await using var db = await P01Async();
        // a database DDL trigger that refuses to drop dbo.Jobs makes the reset fail half-way
        await db.ExecuteAsync("""
            EXEC('CREATE TRIGGER Zz_BlockJobsDrop ON DATABASE FOR DROP_TABLE AS
                  IF EVENTDATA().value(''(/EVENT_INSTANCE/ObjectName)[1]'', ''sysname'') = ''Jobs''
                  BEGIN RAISERROR(''blocked by test trigger'', 16, 1); ROLLBACK; END')
            """);
        var before = await db.FingerprintAsync();

        var run = await Run(db, Reset, apply: true);

        Assert.True(run.Failed);
        Assert.Equal(before, await db.FingerprintAsync());      // every table, row, key and the HangFire schema intact
    }

    [SqlServerFact]
    public async Task Running_the_reset_again_stops_without_changes()
    {
        await using var db = await P01Async();
        Assert.False((await Run(db, Reset, apply: true)).Failed);
        var before = await db.FingerprintAsync();

        var again = await Run(db, Reset, apply: true);

        Assert.True(again.Failed);
        Assert.Contains("already reset", again.Output);
        Assert.Equal(before, await db.FingerprintAsync());
    }

    // ---------------------------------------------------------------- forward path: reset -> baseline -> reference data -> verify -> API

    [SqlServerFact]
    public async Task Forward_path_produces_a_verified_baseline_that_the_api_accepts()
    {
        await using var db = await P01Async();

        Assert.False((await Run(db, Reset, apply: true)).Failed);
        foreach (var _ in new[] { 1, 2 })   // idempotent
        {
            var baseline = await Run(db, Baseline, input: false);
            Assert.False(baseline.Failed, baseline.Output);
        }

        var dryRun = await Run(db, ReferenceData);
        Assert.False(dryRun.Failed, dryRun.Output);
        Assert.Contains("8 of 8 present, 0 to insert", dryRun.Output);
        foreach (var _ in new[] { 1, 2 })   // idempotent, insert-only
        {
            var apply = await Run(db, ReferenceData, apply: true);
            Assert.False(apply.Failed, apply.Output);
            Assert.Contains("inserted 0 categor(ies), 0 platform company, 0 baseline marker", apply.Output);
        }

        var verify = await Run(db, Verify);
        Assert.False(verify.Failed, verify.Output);
        Assert.Contains("VERIFY: PASS", verify.Output);
        Assert.Contains("info accounts: 0", verify.Output);

        await using var api = new ExistingDatabaseApiFactory(db.ConnectionString);
        var client = api.CreateClient();
        Assert.Equal("Healthy", await client.GetStringAsync("/health"));
    }

    [SqlServerFact]
    public async Task Reference_data_script_restores_missing_rows_without_touching_existing_ones()
    {
        await using var db = await ThrowawayDatabase.CreateAsync();
        await ScriptDatabases.BuildBaselineV2Async(db, withHangfire: false);
        await db.ExecuteAsync("""
            DELETE FROM dbo.JobCategories WHERE Id IN (7, 8);
            UPDATE dbo.JobCategories SET Name = 'Renamed by admin' WHERE Id = 1;
            DELETE FROM dbo.Companies WHERE IsPlatform = 1;
            """);

        var run = await Run(db, ReferenceData, apply: true);

        Assert.False(run.Failed, run.Output);
        Assert.Contains("inserted 2 categor(ies), 1 platform company", run.Output);
        Assert.Equal("Renamed by admin", await db.ScalarAsync<string>("SELECT Name FROM dbo.JobCategories WHERE Id = 1"));
        Assert.Equal(PlatformCompany.Id, await db.ScalarAsync<Guid>("SELECT Id FROM dbo.Companies WHERE IsPlatform = 1"));
        Assert.Contains("VERIFY: PASS", (await Run(db, Verify)).Output);
    }

    [SqlServerTheory]
    [InlineData("DROP TRIGGER dbo.TR_AuditLogs_AppendOnly", "trigger")]
    [InlineData("INSERT INTO dbo.__EFMigrationsHistory VALUES ('20990101000000_Unknown', '10.0.14')", "migration history")]
    [InlineData("DROP INDEX UX_RefreshTokens_ActivePerSession ON dbo.RefreshTokens", "filtered unique")]
    [InlineData("DELETE FROM dbo.Companies WHERE IsPlatform = 1", "platform company")]
    [InlineData("CREATE TABLE dbo.CVs (Id int)", "legacy P01 tables")]
    public async Task Verification_fails_on_a_tampered_schema(string tamper, string reason)
    {
        await using var db = await ThrowawayDatabase.CreateAsync();
        await ScriptDatabases.BuildBaselineV2Async(db, withHangfire: false);
        await db.ExecuteAsync(tamper);

        var run = await Run(db, Verify);

        Assert.True(run.Failed);
        Assert.Contains("VERIFY: FAIL", run.Output);
        Assert.Contains(reason, run.Output);
    }

    [SqlServerFact]
    public async Task Reference_data_and_verification_refuse_a_database_that_is_not_baseline_v2()
    {
        await using var db = await P01Async();
        var before = await db.FingerprintAsync();

        var reference = await Run(db, ReferenceData, apply: true);
        var verify = await Run(db, Verify);

        Assert.True(reference.Failed);
        Assert.Contains("STOPPED", reference.Output);
        Assert.True(verify.Failed);
        Assert.Equal(before, await db.FingerprintAsync());
    }

    // ---------------------------------------------------------------- rollback

    [SqlServerFact]
    public async Task Rollback_restores_an_empty_P01_schema_and_the_forward_path_works_again()
    {
        await using var db = await ThrowawayDatabase.CreateAsync();
        await ScriptDatabases.BuildBaselineV2Async(db);
        await db.ExecuteAsync("CREATE TABLE dbo.Zz_Unrelated (Id int); INSERT INTO dbo.Zz_Unrelated VALUES (1);");
        var before = await db.FingerprintAsync();

        var dryRun = await Run(db, RollbackDrop);
        Assert.False(dryRun.Failed, dryRun.Output);
        Assert.Equal(before, await db.FingerprintAsync());

        var drop = await Run(db, RollbackDrop, apply: true);
        Assert.False(drop.Failed, drop.Output);
        Assert.Contains("ROLLBACK STEP 1 APPLIED", drop.Output);
        Assert.Equal(["dbo.Zz_Unrelated"], await db.TablesAsync());

        foreach (var _ in new[] { 1, 2 })   // idempotent
        {
            var p01 = await Run(db, P01Schema, input: false);
            Assert.False(p01.Failed, p01.Output);
        }
        Assert.Equal(string.Join(",", P01Migrations),
            await db.ScalarAsync<string>("SELECT STRING_AGG(MigrationId, ',') WITHIN GROUP (ORDER BY MigrationId) FROM dbo.__EFMigrationsHistory"));
        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.AspNetUsers"));   // empty: no account, fail closed

        // the rolled-back schema equals a freshly created P01 schema
        await using (var fresh = await ThrowawayDatabase.CreateAsync())
        {
            Assert.False((await Run(fresh, P01Schema, input: false)).Failed);
            await fresh.ExecuteAsync("CREATE TABLE dbo.Zz_Unrelated (Id int); INSERT INTO dbo.Zz_Unrelated VALUES (1);");
            Assert.Equal(await fresh.FingerprintAsync(), await db.FingerprintAsync());
        }

        // forward again
        Assert.False((await Run(db, Reset, apply: true)).Failed);
        Assert.False((await Run(db, Baseline, input: false)).Failed);
        Assert.False((await Run(db, ReferenceData, apply: true)).Failed);
        Assert.Contains("VERIFY: PASS", (await Run(db, Verify)).Output);
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT Id FROM dbo.Zz_Unrelated"));
    }

    [SqlServerFact]
    public async Task Rollback_refuses_a_database_that_is_not_baseline_v2_or_not_named()
    {
        await using var p01 = await P01Async();
        var p01Before = await p01.FingerprintAsync();
        var onP01 = await Run(p01, RollbackDrop, apply: true);
        Assert.True(onP01.Failed);
        Assert.Contains("exactly 20261101054955_Baseline_v2", onP01.Output);
        Assert.Equal(p01Before, await p01.FingerprintAsync());

        await using var v2 = await ThrowawayDatabase.CreateAsync();
        await ScriptDatabases.BuildBaselineV2Async(v2);
        var v2Before = await v2.FingerprintAsync();
        var unnamed = await Run(v2, RollbackDrop, expected: "", apply: true);
        Assert.True(unnamed.Failed);
        Assert.Equal(v2Before, await v2.FingerprintAsync());
    }

    // ---------------------------------------------------------------- the scripts match the code

    [Fact]
    public void Every_script_starts_with_the_session_options_header()
    {
        var header = Normalize(SqlScripts.Load("session-options.sql"));
        foreach (var file in new[] { Reset, Baseline, ReferenceData, Verify, RollbackDrop, P01Schema })
            Assert.StartsWith(header, Normalize(SqlScripts.Load(file)));
    }

    [Fact]
    public void Committed_baseline_script_is_the_current_migration()
    {
        using var context = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer("Server=unused").Options);
        var generated = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var committed = SqlScripts.Load(Baseline);

        Assert.Equal(Normalize(SqlScripts.Load("session-options.sql") + "\n" + generated), Normalize(committed));

        var current = context.Database.GetMigrations().Single();
        foreach (var file in new[] { ReferenceData, Verify, RollbackDrop })
            Assert.Contains(current, SqlScripts.Load(file));
    }

    [Fact]
    public void Reference_data_script_matches_the_model_seed()
    {
        using var context = new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer("Server=unused").Options);
        var script = SqlScripts.Load(ReferenceData);

        var categories = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(JobCategory))!.GetSeedData().ToList();
        Assert.Equal(8, categories.Count);
        foreach (var c in categories)
            Assert.Contains($"({c["Id"]}, N'{c["Name"]}', N'{c["Slug"]}')", script);

        var platform = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Company))!.GetSeedData().Single();
        Assert.Contains(((Guid)platform["Id"]!).ToString().ToUpperInvariant(), script);
        Assert.Contains($"N'{platform["Name"]}', N'{platform["Slug"]}'", script);
    }

    private static string Normalize(string s) =>
        string.Join("\n", s.Replace("\r\n", "\n").TrimStart('﻿').Split('\n').Select(l => l.TrimEnd())).Trim();
}

/// <summary>The API pipeline against an existing database (no migration, no seeding by the test).</summary>
public sealed class ExistingDatabaseApiFactory(string connectionString) : ApiFactory
{
    protected override void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDBContext>(o => o.UseSqlServer(connectionString));
}
