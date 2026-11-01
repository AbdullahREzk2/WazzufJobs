using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Audit;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.Shared;

namespace WazzufJobs.Tests.Infrastructure;

/// <summary>
/// Services as a request of <paramref name="UserId"/> would see them, for running handlers directly (outside HTTP):
/// the current user, the audit log that records that user as actor, and company access for that user.
/// </summary>
public sealed record ActingUser(string? UserId) : ICurrentUserService
{
    public IAuditLog Audit(IServiceProvider services) => ActivatorUtilities.CreateInstance<AuditLogService>(services, this);

    public ICompanyAccessService CompanyAccess(IServiceProvider services) =>
        new CompanyAccessService(this, services.GetRequiredService<ICompanyRepository>(), Audit(services));
}
