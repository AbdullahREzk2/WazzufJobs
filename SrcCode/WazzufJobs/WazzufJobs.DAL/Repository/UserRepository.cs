using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.DAL.Repository;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDBContext _dbcontext;
    private readonly UserManager<AppUser> _usermanager;

    public UserRepository(ApplicationDBContext dbContext, UserManager<AppUser> userManager)
    {
        _dbcontext = dbContext;
        _usermanager = userManager;
    }


    public async Task<AppUser?> FindByEmailAsync(string email)
        => await _usermanager.FindByEmailAsync(email);

    public async Task<AppUser?> FindByIdAsync(string userId)
        => await _usermanager.FindByIdAsync(userId);

    public async Task<bool> EmailExistsAsync(string email)
        => await _usermanager.Users.AnyAsync(x => x.Email == email);

    public async Task<bool> EmailExistsForOtherUserAsync(string email, string excludedUserId, CancellationToken cancellationToken)
        => await _usermanager.Users.AnyAsync(x => x.Email == email && x.Id != excludedUserId, cancellationToken);

    public async Task<IList<string>> GetRolesAsync(AppUser user)
        => await _usermanager.GetRolesAsync(user);

    public async Task<IList<string>> GetUserPermissionsAsync(IList<string> userRoles, CancellationToken cancellationToken)
    {
        return (await (
            from r in _dbcontext.Roles
            join p in _dbcontext.RoleClaims on r.Id equals p.RoleId
            where userRoles.Contains(r.Name!)
            select p.ClaimValue)
            .Distinct()
            .ToListAsync(cancellationToken))!;
    }

    public async Task<List<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken)
        => await _usermanager.Users.ToListAsync(cancellationToken);

    public async Task<List<UserWithRoles>> GetUsersWithRolesAsync(CancellationToken cancellationToken)
        => await (
            from u in _dbcontext.Users
            join ur in _dbcontext.UserRoles on u.Id equals ur.UserId
            join r in _dbcontext.Roles on ur.RoleId equals r.Id into roles
            where !roles.Any(x => x.Name == AppRoles.User.Name)
            select new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.IsDisabled,
                Roles = roles.Select(x => x.Name!)
            }
        )
        .GroupBy(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.IsDisabled })
        .Select(u => new UserWithRoles(
            u.Key.Id,
            u.Key.FirstName,
            u.Key.LastName,
            u.Key.Email!,
            u.Key.IsDisabled,
            u.SelectMany(x => x.Roles).Distinct()
        ))
        .ToListAsync(cancellationToken);


    public async Task<IdentityResult> CreateAsync(AppUser user, string password)
        => await _usermanager.CreateAsync(user, password);

    public async Task<IdentityResult> UpdateAsync(AppUser user)
        => await _usermanager.UpdateAsync(user);

    public async Task UpdateUserProfileAsync(string userId, string firstName, string lastName)
        => await _usermanager.Users
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.FirstName, firstName)
                .SetProperty(p => p.LastName, lastName)
            );

    public async Task<IdentityResult> AddToRoleAsync(AppUser user, string role)
        => await _usermanager.AddToRoleAsync(user, role);

    public async Task<IdentityResult> AddToRolesAsync(AppUser user, IEnumerable<string> roles)
        => await _usermanager.AddToRolesAsync(user, roles);

    public async Task DeleteUserRolesAsync(string userId, CancellationToken cancellationToken)
        => await _dbcontext.UserRoles
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IdentityResult> ReplaceUserRolesAsync(AppUser user, IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var target = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        async Task<IdentityResult> ApplyDifferencesAsync()
        {
            var current = await _usermanager.GetRolesAsync(user);
            var toRemove = current.Where(r => !target.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
            var toAdd = target.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

            if (toRemove.Count > 0)
            {
                var removed = await _usermanager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                    return removed;
            }

            return toAdd.Count > 0
                ? await _usermanager.AddToRolesAsync(user, toAdd)
                : IdentityResult.Success;
        }

        if (!_dbcontext.Database.IsRelational())
            return await ApplyDifferencesAsync();

        // the context uses a retrying execution strategy, so the transaction must run inside it
        var strategy = _dbcontext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbcontext.Database.BeginTransactionAsync(cancellationToken);
            var result = await ApplyDifferencesAsync();

            if (result.Succeeded)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);

            return result;
        });
    }

    public async Task<int> CountActiveUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        => (await _usermanager.GetUsersInRoleAsync(roleName)).Count(u => !u.IsDisabled);

    public async Task<IdentityResult> SetLockoutEndDateAsync(AppUser user, DateTimeOffset? lockoutEnd)
        => await _usermanager.SetLockoutEndDateAsync(user, lockoutEnd);


    public async Task<string> GenerateEmailConfirmationTokenAsync(AppUser user)
        => await _usermanager.GenerateEmailConfirmationTokenAsync(user);

    public async Task<IdentityResult> ConfirmEmailAsync(AppUser user, string code)
        => await _usermanager.ConfirmEmailAsync(user, code);


    public async Task<string> GeneratePasswordResetTokenAsync(AppUser user)
        => await _usermanager.GeneratePasswordResetTokenAsync(user);

    public async Task<IdentityResult> ResetPasswordAsync(AppUser user, string code, string newPassword)
        => await _usermanager.ResetPasswordAsync(user, code, newPassword);

    public async Task<IdentityResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword)
        => await _usermanager.ChangePasswordAsync(user, currentPassword, newPassword);

    public async Task<int> RevokeAllRefreshTokensAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _dbcontext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedOn == null && t.ExpiresOn > now)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.RevokedOn = now;

        await _dbcontext.SaveChangesAsync(cancellationToken);
        return activeTokens.Count;
    }

    public async Task<AppUser?> FindByIdWithProfileAsync(string userId, CancellationToken cancellationToken) =>
        await _dbcontext.Users
            .Include(u => u.CandidateProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
}