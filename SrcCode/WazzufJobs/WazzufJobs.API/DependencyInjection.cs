using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WazzufJobs.API.Services;
using WazzufJobs.API.Setting;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.BLL.Behaviors;
using WazzufJobs.BLL.Errors;
using WazzufJobs.BLL.Helpers;
using WazzufJobs.BLL.Services;
using WazzufJobs.BLL.Services.Storage;
using WazzufJobs.BLL.Setting;
using WazzufJobs.BLL.Settings;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Repository;
using WazzufJobs.Shared;

namespace WazzufJobs.API;

public static class DependencyInjection
{
    
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDBContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        // Phase 02 (S2a): data partition for the demo-isolation query filters; real data only until demo is enabled
        services.AddScoped<IDataScope, DefaultDataScope>();

        services.Configure<AppURLSetting>(
                configuration.GetSection("AppURL"));

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<AppUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDBContext>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 8;
            options.SignIn.RequireConfirmedEmail = true;
            options.User.RequireUniqueEmail = true;
        });
        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtSettings = configuration
            .GetSection(JwtOptions.sectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT settings not configured.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.SaveToken = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.key!)),
                // Phase 02 (S3): authorization is by permission only. No token carries this claim type, so role checks
                // (IsInRole, [Authorize(Roles = ...)]) never succeed and the legacy aliases in "roles" grant nothing.
                RoleClaimType = AccessTokenClaims.NoRoleAuthorization
            };

            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddSingleton<IJwtProvider, JwtProvider>();

        return services;
    }
    public static IServiceCollection AddMediatRServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IJwtProvider).Assembly);
            // Phase 02 S1: run the request's FluentValidation validators before its handler
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }

    public static IServiceCollection AddMapsterServices(this IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(IJwtProvider).Assembly);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }

    public static IServiceCollection AddFluentValidationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(IJwtProvider).Assembly);
        return services;
    }

    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionsHandler>();
        services.AddProblemDetails();
        return services;
    }

    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));

        services.AddHangfireServer();
        return services;
    }

    public static IServiceCollection AddCloudinaryServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CloudinarySettings>()
            .BindConfiguration(CloudinarySettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // public images (profile photos, category icons)
        services.AddScoped<ICloudinaryService, CloudinaryService>();

        // private documents (CVs) behind a provider-neutral abstraction
        var storage = configuration.GetSection(StorageSettings.SectionName).Get<StorageSettings>() ?? new StorageSettings();
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
        switch (storage.DocumentProvider)
        {
            case "Cloudinary":
                services.AddHttpClient(CloudinaryDocumentStorage.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
                services.AddScoped<IDocumentStorage, CloudinaryDocumentStorage>();
                break;
            default:
                // fail closed: never fall back to an unintended (possibly public) storage
                throw new InvalidOperationException($"Unsupported Storage:DocumentProvider '{storage.DocumentProvider}'.");
        }

        return services;
    }

    public static IServiceCollection AddMailServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MailSettings>()
            .BindConfiguration(nameof(MailSettings))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IEmailSender, EmailService>();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ISavedJobRepository, SavedJobRepository>();  
        return services;
    }

    public static IServiceCollection AddHelperServices(this IServiceCollection services)
    {

        services.AddScoped<ISendConfirmationEmailHelper, SendConfirmationEmailHelper>();
        services.AddScoped<ISendResetPasswordEmailHelper, SendResetPasswordEmailHelper>();
        services.AddScoped<IsendWelcomeEmail, sendWelcomeEmail>();

        // acting user of the current request (e.g. who changed an application status)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // company-level authorization (Phase 02, S4)
        services.AddScoped<ICompanyAccessService, CompanyAccessService>();

        return services;
    }

    public static IServiceCollection AddAIServices(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddOptions<AISettings>()
            .BindConfiguration(AISettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ICVTextExtractor, CVTextExtractor>();
        services.AddHttpClient<IAIClient, GroqAIClient>();    
        services.AddScoped<IAIScoringService, AIScoringService>();

        return services;
    }

    public static IServiceCollection AddSignalRServices(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    public static IServiceCollection AddFeatureFlags(this IServiceCollection services)
    {
        services.AddOptions<FeatureSettings>()
            .BindConfiguration(FeatureSettings.SectionName);

        return services;
    }

    public const string AuthRateLimitPolicy = "ipLimit";

    public static IServiceCollection AddRateLimitingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimitPerMinute", 20);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per-client-IP fixed window for anonymous authentication endpoints (login, register, reset, ...).
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services.AddSingleton<SchemaCompatibility>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
