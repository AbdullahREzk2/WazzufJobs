using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Entities;
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

    public async Task<CompanyMembership?> FindActiveMembershipAsync(Guid companyId, string userId, CancellationToken cancellationToken) =>
        await (
            from m in _context.CompanyMembers
            join c in _context.Companies on m.CompanyId equals c.Id
            join u in _context.Users on m.UserId equals u.Id
            where m.CompanyId == companyId && m.UserId == userId && m.Status == CompanyMemberStatus.Active
                && !c.IsPlatform
                && u.AccountType == AccountType.Company && !u.IsDisabled && u.IsDemo == c.IsDemo
            select new CompanyMembership(m.CompanyId, m.UserId, m.Role, c.Status))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CompanyProfile?> GetProfileAsync(Guid companyId, CancellationToken cancellationToken) =>
        await _context.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyProfile(c.Id, c.Name, c.Slug, c.Website, c.Industry, c.Size, c.Location, c.Description,
                c.VerificationStatus, c.Status, c.CreatedAt))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<List<CompanyMemberSummary>> GetActiveMembersAsync(Guid companyId, CancellationToken cancellationToken) =>
        await (
            from m in _context.CompanyMembers
            join u in _context.Users on m.UserId equals u.Id
            where m.CompanyId == companyId && m.Status == CompanyMemberStatus.Active
            orderby m.Role, u.LastName, u.FirstName
            select new CompanyMemberSummary(m.UserId, u.FirstName, u.LastName, u.Email!, m.Role, m.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<CompanyMemberState?> GetActiveMemberAsync(Guid companyId, string userId, CancellationToken cancellationToken) =>
        await (
            from m in _context.CompanyMembers
            join u in _context.Users on m.UserId equals u.Id
            where m.CompanyId == companyId && m.UserId == userId && m.Status == CompanyMemberStatus.Active
            select new CompanyMemberState(m.Role, u.IsDisabled))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> CountActiveOwnersAsync(Guid companyId, CancellationToken cancellationToken) =>
        await (
            from m in _context.CompanyMembers
            join u in _context.Users on m.UserId equals u.Id
            where m.CompanyId == companyId && m.Status == CompanyMemberStatus.Active && m.Role == CompanyMemberRole.Owner && !u.IsDisabled
            select m.UserId)
            .CountAsync(cancellationToken);

    public Task<bool> SetMemberRoleAsync(Guid companyId, string userId, CompanyMemberRole role, CancellationToken cancellationToken) =>
        UpdateActiveMemberAsync(companyId, userId, m => m.Role = role, cancellationToken);

    public Task<bool> RemoveMemberAsync(Guid companyId, string userId, CancellationToken cancellationToken) =>
        UpdateActiveMemberAsync(companyId, userId, m => m.Status = CompanyMemberStatus.Removed, cancellationToken);

    private async Task<bool> UpdateActiveMemberAsync(Guid companyId, string userId, Action<CompanyMember> change, CancellationToken cancellationToken)
    {
        var member = await _context.CompanyMembers
            .SingleOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == userId && m.Status == CompanyMemberStatus.Active, cancellationToken);
        if (member is null)
            return false;

        change(member);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TResult> RunExclusiveForCompanyAsync<TResult>(
        Guid companyId, Func<Task<TResult>> change, Func<TResult, bool> succeeded, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return await change();

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // the company row is the lock: every membership change of this company takes it first (U locks conflict
            // with each other but not with readers)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @locked int; SELECT @locked = 1 FROM [Companies] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {companyId};",
                cancellationToken);

            var result = await change();
            if (succeeded(result))
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);
            return result;
        });
    }
}
