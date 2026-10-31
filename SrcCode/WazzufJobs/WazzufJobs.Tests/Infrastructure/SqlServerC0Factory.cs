using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// Same pipeline as <see cref="ApiFactory"/>, backed by a THROWAWAY database on a local SQL Server
/// (needed for relational behaviour such as transactions, ExecuteDelete and concurrency). The database is
/// uniquely named "<see cref="DatabasePrefix"/>*", created from the EF model and dropped on dispose; the drop is
/// refused for any database without that prefix. No other database is touched.
/// Connection: env WAZZUF_TEST_SQLSERVER or "Server=.;Trusted_Connection=True;TrustServerCertificate=True".
/// </summary>
public class SqlServerApiFactory : ApiFactory
{
    public static string ServerConnection =>
        Environment.GetEnvironmentVariable("WAZZUF_TEST_SQLSERVER")
        ?? "Server=.;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

    private readonly string _connectionString;

    /// <summary>Prefix of the throwaway database name; must start with "WazzufJobs_" and end with "Test_".</summary>
    protected virtual string DatabasePrefix => "WazzufJobs_ApiTest_";

    public SqlServerApiFactory()
    {
        var builder = new SqlConnectionStringBuilder(ServerConnection)
        {
            InitialCatalog = $"{DatabasePrefix}{Guid.NewGuid():N}"
        };
        _connectionString = builder.ConnectionString;

        if (!SqlServerFactAttribute.IsAvailable)
            return; // tests using this factory are skipped

        // create schema + HasData seed (roles, role permissions, categories) before the host starts
        using var context = new ApplicationDBContext(Options());
        context.Database.EnsureCreated();
    }

    public string ConnectionString => _connectionString;

    protected override void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDBContext>(o => o.UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure()));

    private DbContextOptions<ApplicationDBContext> Options() =>
        new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(_connectionString).Options;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !SqlServerFactAttribute.IsAvailable) return;

        using var context = new ApplicationDBContext(Options());
        if (context.Database.GetDbConnection().Database.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            context.Database.EnsureDeleted();
    }
}

/// <summary>The Phase C0 relational test host (database prefix unchanged: "WazzufJobs_C0Test_").</summary>
public class SqlServerC0Factory : SqlServerApiFactory
{
    protected override string DatabasePrefix => "WazzufJobs_C0Test_";
}

/// <summary>A fact that is skipped (not failed) when no local SQL Server is reachable.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public static bool IsAvailable => Available.Value;

    private static readonly Lazy<bool> Available = new(() =>
    {
        try
        {
            using var connection = new SqlConnection(SqlServerApiFactory.ServerConnection);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    });

    public SqlServerFactAttribute()
    {
        if (!Available.Value)
            Skip = "Local SQL Server not reachable (set WAZZUF_TEST_SQLSERVER to run relational tests).";
    }
}
