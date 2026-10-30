using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using WazzufJobs.API.Services;
using WazzufJobs.BLL.Hubs;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ─────────
        builder.Services.AddDatabase(builder.Configuration);
        builder.Services.AddIdentityServices();
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddMediatRServices();
        builder.Services.AddMapsterServices();
        builder.Services.AddFluentValidationServices();
        builder.Services.AddExceptionHandling();
        builder.Services.AddHangfireServices(builder.Configuration);
        builder.Services.AddCloudinaryServices(builder.Configuration);
        builder.Services.AddMailServices(builder.Configuration);
        builder.Services.AddRepositories();
        builder.Services.AddHelperServices();
        builder.Services.AddAIServices(builder.Configuration);
        builder.Services.AddSignalRServices();
        builder.Services.AddFeatureFlags();
        builder.Services.AddRateLimitingServices(builder.Configuration);
        builder.Services.AddHealthCheckServices();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:4200",
                        "https://wazzuf-jobs.vercel.app"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
        // ─────────────────────────────────────────────────

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Wazzuf Jobs API",
                Version = "v1"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token. Example: eyJhbGci..."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
        });

        var app = builder.Build();

        LifecycleLog.Register(app);

        // ── Seed data ────────────────────────────────────
        await SeedDataAsync(app);
        // ─────────────────────────────────────────────────

        // API documentation is only exposed in Development (Phase C0, S13).
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "WazzufJobs API V1");
                c.RoutePrefix = string.Empty;
            });
        }

        // The Hangfire dashboard is intentionally not mapped (Phase C0, S5): background
        // processing still runs, but the dashboard is not reachable over HTTP.
        app.UseExceptionHandler();
        app.UseCors("AllowFrontend");
        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapHub<ScoringHub>("/hubs/scoring");
        app.MapControllers();
        await app.RunAsync();
    }

    private static async Task SeedDataAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();

            await RoleSeeder.SeedAsync(roleManager);

            // Admin bootstrap credentials come only from host configuration (never from source control).
            var outcome = await AdminSeeder.SeedAsync(
                userManager,
                app.Configuration["AdminBootstrap:Email"],
                app.Configuration["AdminBootstrap:Password"]);

            logger.LogInformation("Admin bootstrap: {Outcome}.", outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during seeding.");
        }
    }
}
