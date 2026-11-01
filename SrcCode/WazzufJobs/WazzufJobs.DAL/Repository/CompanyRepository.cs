using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.Enums;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;

namespace WazzufJobs.DAL.Repository;

public class CompanyRepository(ApplicationDBContext context) : ICompanyRepository
{
    private readonly ApplicationDBContext _context = context;

    public async Task<Guid?> GetPlatformCompanyIdAsync(CancellationToken cancellationToken) =>
        await _context.Companies
            .Where(c => c.IsPlatform && c.Status == CompanyStatus.Active)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
