using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.API.Services;

public enum SchemaStatus
{
    /// <summary>Not checked yet.</summary>
    Unknown,

    /// <summary>The database holds exactly the migrations compiled into this build.</summary>
    Compatible,

    /// <summary>Non-relational store (in-memory test database): migrations do not apply.</summary>
    NotApplicable,

    /// <summary>Migrations history missing, behind, ahead of, or different from this build.</summary>
    Incompatible,

    /// <summary>The database could not be reached; checked again later.</summary>
    Unavailable
}

/// <summary>
/// Fail-closed schema compatibility guard (Phase 02, §9; invariant P02-11). The API serves requests only when the
/// database's applied migrations equal, in order, the migrations compiled into this build. Otherwise every endpoint
/// except <c>/health</c> returns 503 and <c>/health</c> reports Unhealthy. The guard never migrates the database:
/// schema changes are applied only by reviewed scripts.
/// </summary>
public sealed class SchemaCompatibility(IServiceScopeFactory scopeFactory, ILogger<SchemaCompatibility> logger)
{
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile SchemaStatus _status = SchemaStatus.Unknown;
    private DateTime _checkedAtUtc = DateTime.MinValue;

    public SchemaStatus Status => _status;

    /// <summary>Latest migration compiled into this build (for the lifecycle log).</summary>
    public string? ExpectedMigration { get; private set; }

    public bool IsOperational => _status is SchemaStatus.Compatible or SchemaStatus.NotApplicable;

    /// <summary>Checks now (startup), regardless of the re-check interval.</summary>
    public async Task<SchemaStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await CheckCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// True when requests may be served. A non-operational state is re-checked at most every 15 seconds, so a
    /// database that was unreachable at startup recovers without a restart; an incompatible schema stays closed.
    /// </summary>
    public async Task<bool> EnsureOperationalAsync(CancellationToken cancellationToken = default)
    {
        if (IsOperational)
            return true;
        if (DateTime.UtcNow - _checkedAtUtc < RecheckInterval)
            return false;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsOperational && DateTime.UtcNow - _checkedAtUtc >= RecheckInterval)
                await CheckCoreAsync(cancellationToken);
            return IsOperational;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SchemaStatus> CheckCoreAsync(CancellationToken cancellationToken)
    {
        SchemaStatus status;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>().Database;

            if (!database.IsRelational())
            {
                status = SchemaStatus.NotApplicable;
            }
            else
            {
                var expected = database.GetMigrations().ToList();
                ExpectedMigration = expected.LastOrDefault();

                if (!await database.CanConnectAsync(cancellationToken))
                {
                    status = SchemaStatus.Unavailable;
                    logger.LogWarning("Schema check: database unreachable; requests are refused until it is reachable.");
                }
                else
                {
                    var applied = (await database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
                    status = applied.SequenceEqual(expected, StringComparer.Ordinal)
                        ? SchemaStatus.Compatible
                        : SchemaStatus.Incompatible;

                    if (status == SchemaStatus.Incompatible)
                        logger.LogError(
                            "Schema check FAILED (fail closed): database has {AppliedCount} applied migration(s), this build expects {ExpectedCount} ending with {Expected}; " +
                            "unknown in database: {Unknown}; missing in database: {Missing}. No schema change is made automatically.",
                            applied.Count, expected.Count, ExpectedMigration,
                            applied.Except(expected).Count(), expected.Except(applied).Count());
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // exception type only: messages can contain server names
            status = SchemaStatus.Unavailable;
            logger.LogWarning("Schema check could not complete ({ExceptionType}); requests are refused until it succeeds.", ex.GetType().Name);
        }

        _status = status;
        _checkedAtUtc = DateTime.UtcNow;
        return status;
    }
}

/// <summary>Refuses every request except /health while the schema is not operational (503, no details).</summary>
public static class SchemaGuardMiddleware
{
    public const string IncompatibleCode = "Schema.Incompatible";
    public const string UnavailableCode = "Database.Unavailable";

    public static IApplicationBuilder UseSchemaGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await next();
                return;
            }

            var guard = context.RequestServices.GetRequiredService<SchemaCompatibility>();
            if (await guard.EnsureOperationalAsync(context.RequestAborted))
            {
                await next();
                return;
            }

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service temporarily unavailable.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.4"
            };
            problem.Extensions["code"] = guard.Status == SchemaStatus.Incompatible ? IncompatibleCode : UnavailableCode;

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "30";
            await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
                contentType: "application/problem+json", context.RequestAborted);
        });
}
