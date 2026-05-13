using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using WazzufJobs.API.Filters;
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

        // ── Seed data ────────────────────────────────────
        await SeedDataAsync(app);
        // ─────────────────────────────────────────────────

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "WazzufJobs API V1");
                c.RoutePrefix = string.Empty;
            });
        }
        app.UseHangfireDashboard("/hangfire");
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireAuthorizationFilter()]
        });
        app.MapControllers();
        await app.RunAsync();
    }

    private static async Task SeedDataAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<AppUser>>();

        await RoleSeeder.SeedAsync(roleManager);
        await AdminSeeder.SeedAsync(userManager);
    }
}