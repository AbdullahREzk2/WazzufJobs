using Microsoft.Extensions.Diagnostics.HealthChecks;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.API.Services;

/// <summary>Reports whether the database is reachable. The response contains no details.</summary>
public class DatabaseHealthCheck(ApplicationDBContext context) : IHealthCheck
{
    private readonly ApplicationDBContext _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
