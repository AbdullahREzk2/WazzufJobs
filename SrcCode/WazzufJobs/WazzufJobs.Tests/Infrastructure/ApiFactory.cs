using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// Shared integration-test host (Phase 02 generalization of the C0 factory). Hosts the real API pipeline
/// (controllers, authentication, permission policies, rate limiter, health checks) in the Production
/// environment, with the database replaced by EF InMemory and Cloudinary, SMTP and Hangfire replaced by fakes.
/// No external service is ever contacted.
/// Extension points: <see cref="ConfigureSettings"/> (configuration values), <see cref="ConfigureDatabase"/>
/// (store), <see cref="ConfigureServices"/> (service overrides) and <see cref="ConfigureAdditional"/> (host builder).
/// </summary>
public class ApiFactory : WebApplicationFactory<WazzufJobs.API.Program>
{
    private readonly string _databaseName = $"wazzuf-api-{Guid.NewGuid():N}";

    public FakeDocumentStorage Storage { get; } = new();
    public FakeImageService Images { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public FakeBackgroundJobClient BackgroundJobs { get; } = new();

    public const string IgnoredBootstrapEmail = "bootstrap-attempt@example.test";

    protected virtual bool CvUploadsEnabled => false;
    protected virtual int AuthPermitLimitPerMinute => 1000;

    /// <summary>Default: an isolated in-memory store per factory.</summary>
    protected virtual void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDBContext>(o => o.UseInMemoryDatabase(_databaseName));

    /// <summary>
    /// Test-only configuration. Not real secrets: nothing here can reach a real service.
    /// Override and call base to add or replace values.
    /// </summary>
    protected virtual void ConfigureSettings(IDictionary<string, string?> settings)
    {
        settings["ConnectionStrings:DefaultConnection"] = "Server=unused-in-tests;Database=none";
        settings["Jwt:key"] = "c0-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";
        // test-only audit key (base64 of the 32 ASCII bytes "wazzufjobs-test-only-audit-key!!"); real keys are owner-provided
        settings["Security:DataKey"] = "d2F6enVmam9icy10ZXN0LW9ubHktYXVkaXQta2V5ISE=";
        settings["Jwt:Issuer"] = "WazzufJobsAPI";
        settings["Jwt:Audience"] = "WazzufJobsClient";
        settings["Jwt:ExpiryMinutes"] = "30";
        settings["MailSettings:Host"] = "localhost";
        settings["MailSettings:Port"] = "587";
        settings["MailSettings:Username"] = "test";
        settings["MailSettings:Password"] = "test";
        settings["MailSettings:FromEmail"] = "noreply@example.test";
        settings["MailSettings:FromName"] = "WazzufJobs Tests";
        settings["CloudinarySettings:CloudName"] = "test";
        settings["CloudinarySettings:ApiKey"] = "test";
        settings["CloudinarySettings:ApiSecret"] = "test";
        settings["AISettings:ApiKey"] = "test";
        settings["AppURL:BaseUrl"] = "https://api.example.test";
        settings["AppURL:FrontendUrl"] = "https://app.example.test";
        // former bootstrap settings are deliberately present: they must be ignored (no automatic admin creation)
        settings["AdminBootstrap:Email"] = IgnoredBootstrapEmail;
        settings["AdminBootstrap:Password"] = "Ignored#Passw0rd!1";
        settings["Features:CvUploadsEnabled"] = CvUploadsEnabled.ToString();
        settings["RateLimiting:AuthPermitLimitPerMinute"] = AuthPermitLimitPerMinute.ToString();
        settings["Diagnostics:LifecycleLogEnabled"] = "false";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        ConfigureSettings(settings);
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDBContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDBContext>));
            ConfigureDatabase(services);

            // Hangfire server would need SQL Server storage; jobs are captured by a fake client instead
            var hangfireHostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            ((d.ImplementationType?.Namespace ?? d.ImplementationFactory?.Method.ReturnType.Namespace ?? string.Empty)
                                .StartsWith("Hangfire", StringComparison.Ordinal)))
                .ToList();
            foreach (var descriptor in hangfireHostedServices)
                services.Remove(descriptor);

            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient>(BackgroundJobs);

            services.RemoveAll<ICloudinaryService>();
            services.AddSingleton<ICloudinaryService>(Images);

            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            ConfigureServices(services);
        });

        ConfigureAdditional(builder);
    }

    /// <summary>Hook for service overrides applied after the standard fakes.</summary>
    protected virtual void ConfigureServices(IServiceCollection services) { }

    /// <summary>Hook for factories that need extra host-builder settings.</summary>
    protected virtual void ConfigureAdditional(IWebHostBuilder builder) { }
}
