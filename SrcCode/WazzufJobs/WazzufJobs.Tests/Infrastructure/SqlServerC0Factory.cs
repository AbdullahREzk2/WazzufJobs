using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// Same pipeline as <see cref="C0WebApplicationFactory"/>, backed by a THROWAWAY database on a local SQL Server
/// (needed for relational behaviour such as transactions and ExecuteDelete). The database is uniquely named
/// "WazzufJobs_C0Test_*", created from the EF model and dropped on dispose. No other database is touched.
/// Connection: env WAZZUF_TEST_SQLSERVER or "Server=.;Trusted_Connection=True;TrustServerCertificate=True".
/// </summary>
public class SqlServerC0Factory : C0WebApplicationFactory
{
    public static string ServerConnection =>
        Environment.GetEnvironmentVariable("WAZZUF_TEST_SQLSERVER")
        ?? "Server=.;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

    private readonly string _connectionString;

    public SqlServerC0Factory()
    {
        var builder = new SqlConnectionStringBuilder(ServerConnection)
        {
            InitialCatalog = $"WazzufJobs_C0Test_{Guid.NewGuid():N}"
        };
        _connectionString = builder.ConnectionString;

        if (!SqlServerFactAttribute.IsAvailable)
            return; // tests using this factory are skipped

        // create schema + HasData seed (roles, role permissions, categories) before the host starts
        using var context = new ApplicationDBContext(Options());
        context.Database.EnsureCreated();
    }

    protected override void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDBContext>(o => o.UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure()));

    private DbContextOptions<ApplicationDBContext> Options() =>
        new DbContextOptionsBuilder<ApplicationDBContext>().UseSqlServer(_connectionString).Options;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !SqlServerFactAttribute.IsAvailable) return;

        using var context = new ApplicationDBContext(Options());
        if (context.Database.GetDbConnection().Database.StartsWith("WazzufJobs_C0Test_", StringComparison.Ordinal))
            context.Database.EnsureDeleted();
    }
}

/// <summary>A fact that is skipped (not failed) when no local SQL Server is reachable.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public static bool IsAvailable => Available.Value;

    private static readonly Lazy<bool> Available = new(() =>
    {
        try
        {
            using var connection = new SqlConnection(SqlServerC0Factory.ServerConnection);
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
