using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.API.Services;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>A schema created without migration history (e.g. EnsureCreated or a hand-built database).</summary>
public class NoMigrationHistoryFactory : SqlServerApiFactory
{
    protected override string DatabasePrefix => "WazzufJobs_GuardTest_";
    protected override void PrepareDatabase(ApplicationDBContext context) => context.Database.EnsureCreated();
}

/// <summary>A database migrated by a newer (unknown) build.</summary>
public class UnknownMigrationFactory : SqlServerApiFactory
{
    protected override string DatabasePrefix => "WazzufJobs_GuardTest_";
    protected override void PrepareDatabase(ApplicationDBContext context)
    {
        context.Database.Migrate();
        context.Database.ExecuteSqlRaw(
            "INSERT INTO [__EFMigrationsHistory] (MigrationId, ProductVersion) VALUES ('29991231000000_FutureChange', '10.0.14')");
    }
}

/// <summary>A database whose history does not contain the migration this build expects (an old or foreign schema).</summary>
public class MissingMigrationFactory : SqlServerApiFactory
{
    protected override string DatabasePrefix => "WazzufJobs_GuardTest_";
    protected override void PrepareDatabase(ApplicationDBContext context)
    {
        context.Database.Migrate();
        context.Database.ExecuteSqlRaw("DELETE FROM [__EFMigrationsHistory]");
        context.Database.ExecuteSqlRaw(
            "INSERT INTO [__EFMigrationsHistory] (MigrationId, ProductVersion) VALUES ('20260429160411_initailCreate', '9.0.0')");
    }
}

/// <summary>Fail-closed schema compatibility guard (Phase 02, S2a; invariant P02-11).</summary>
public abstract class SchemaGuardRefusesTests<TFactory>(TFactory factory) : IClassFixture<TFactory>
    where TFactory : SqlServerApiFactory
{
    /// <summary>Migration history rows the test database was prepared with; the guard must leave them untouched.</summary>
    protected abstract int PreparedHistoryRows { get; }

    [SqlServerFact]
    public async Task Health_reports_unhealthy_without_details()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [SqlServerTheory]
    [InlineData("GET", "/api/Jobs")]
    [InlineData("POST", "/api/Auth/login")]
    [InlineData("GET", "/api/Applications/my-applications")]
    [InlineData("GET", "/api/Users")]
    public async Task Every_other_endpoint_is_refused_with_503(string method, string url)
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(SchemaGuardMiddleware.IncompatibleCode, JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("Baseline", body);         // no migration names, no internals
        Assert.DoesNotContain("__EFMigrationsHistory", body);
    }

    [SqlServerFact]
    public void Guard_reports_incompatible_and_never_migrates_the_database()
    {
        Assert.Equal(SchemaStatus.Incompatible, factory.Services.GetRequiredService<SchemaCompatibility>().Status);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        Assert.Equal(PreparedHistoryRows, db.Database.GetAppliedMigrations().Count());
    }
}

public class SchemaGuardWithoutHistoryTests(NoMigrationHistoryFactory factory) : SchemaGuardRefusesTests<NoMigrationHistoryFactory>(factory)
{
    protected override int PreparedHistoryRows => 0;
}

public class SchemaGuardUnknownMigrationTests(UnknownMigrationFactory factory) : SchemaGuardRefusesTests<UnknownMigrationFactory>(factory)
{
    protected override int PreparedHistoryRows => 2;
}

public class SchemaGuardOldSchemaTests(MissingMigrationFactory factory) : SchemaGuardRefusesTests<MissingMigrationFactory>(factory)
{
    protected override int PreparedHistoryRows => 1;
}

/// <summary>The compatible case: the migrated database is served normally.</summary>
public class SchemaGuardCompatibleTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [SqlServerFact]
    public async Task Migrated_database_is_healthy_and_served()
    {
        var client = factory.CreateClient();

        Assert.Equal("Healthy", await client.GetStringAsync("/health"));
        Assert.Equal(SchemaStatus.Compatible, factory.Services.GetRequiredService<SchemaCompatibility>().Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Applications/my-applications")).StatusCode);
    }
}
