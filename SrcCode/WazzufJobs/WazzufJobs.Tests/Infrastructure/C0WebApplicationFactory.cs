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
/// Hosts the real API pipeline (controllers, authentication, permission policies, rate limiter,
/// health checks) in the Production environment, with the database replaced by EF InMemory and
/// Cloudinary, SMTP and Hangfire replaced by fakes. No external service is ever contacted.
/// </summary>
public class C0WebApplicationFactory : WebApplicationFactory<WazzufJobs.API.Program>
{
    private readonly string _databaseName = $"wazzuf-c0-{Guid.NewGuid():N}";

    public FakeDocumentStorage Storage { get; } = new();
    public FakeImageService Images { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public FakeBackgroundJobClient BackgroundJobs { get; } = new();

    public const string IgnoredBootstrapEmail = "bootstrap-attempt@example.test";

    protected virtual bool CvUploadsEnabled => false;

    /// <summary>Default: an isolated in-memory store per factory.</summary>
    protected virtual void ConfigureDatabase(IServiceCollection services) =>
        services.AddDbContext<ApplicationDBContext>(o => o.UseInMemoryDatabase(_databaseName));
    protected virtual int AuthPermitLimitPerMinute => 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // Test-only values. Not real secrets: nothing here can reach a real service.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused-in-tests;Database=none");
        builder.UseSetting("Jwt:key", "c0-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Jwt:Issuer", "WazzufJobsAPI");
        builder.UseSetting("Jwt:Audience", "WazzufJobsClient");
        builder.UseSetting("Jwt:ExpiryMinutes", "30");
        builder.UseSetting("MailSettings:Host", "localhost");
        builder.UseSetting("MailSettings:Port", "587");
        builder.UseSetting("MailSettings:Username", "test");
        builder.UseSetting("MailSettings:Password", "test");
        builder.UseSetting("MailSettings:FromEmail", "noreply@example.test");
        builder.UseSetting("MailSettings:FromName", "WazzufJobs Tests");
        builder.UseSetting("CloudinarySettings:CloudName", "test");
        builder.UseSetting("CloudinarySettings:ApiKey", "test");
        builder.UseSetting("CloudinarySettings:ApiSecret", "test");
        builder.UseSetting("AISettings:ApiKey", "test");
        builder.UseSetting("AppURL:BaseUrl", "https://api.example.test");
        builder.UseSetting("AppURL:FrontendUrl", "https://app.example.test");
        // former bootstrap settings are deliberately present: they must be ignored (no automatic admin creation)
        builder.UseSetting("AdminBootstrap:Email", IgnoredBootstrapEmail);
        builder.UseSetting("AdminBootstrap:Password", "Ignored#Passw0rd!1");
        builder.UseSetting("Features:CvUploadsEnabled", CvUploadsEnabled.ToString());
        builder.UseSetting("RateLimiting:AuthPermitLimitPerMinute", AuthPermitLimitPerMinute.ToString());
        builder.UseSetting("Diagnostics:LifecycleLogEnabled", "false");

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
        });

        ConfigureAdditional(builder);
    }

    /// <summary>Hook for factories that need extra settings or service overrides.</summary>
    protected virtual void ConfigureAdditional(IWebHostBuilder builder) { }
}

public class CvUploadsEnabledFactory : C0WebApplicationFactory
{
    protected override bool CvUploadsEnabled => true;
}

public class StrictRateLimitFactory : C0WebApplicationFactory
{
    public const int Limit = 3;
    protected override int AuthPermitLimitPerMinute => Limit;
}
