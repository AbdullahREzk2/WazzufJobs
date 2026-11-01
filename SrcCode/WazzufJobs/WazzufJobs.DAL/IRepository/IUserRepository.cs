using Microsoft.AspNetCore.Identity;
using WazzufJobs.DAL.Authorization;
using WazzufJobs.DAL.DTOS;
using WazzufJobs.DAL.Entities;


namespace WazzufJobs.DAL.IRepository;

public interface IUserRepository
{
    // Queries
    Task<AppUser?> FindByEmailAsync(string email);
    Task<AppUser?> FindByIdAsync(string userId);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> EmailExistsForOtherUserAsync(string email, string excludedUserId, CancellationToken cancellationToken);
    Task<IList<string>> GetRolesAsync(AppUser user);
    Task<List<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<List<UserWithRoles>> GetUsersWithRolesAsync(CancellationToken cancellationToken);

    // User lifecycle
    Task<IdentityResult> CreateAsync(AppUser user, string password);
    Task<IdentityResult> UpdateAsync(AppUser user);
    Task<IdentityResult> AddToRoleAsync(AppUser user, string role);
    Task<IdentityResult> AddToRolesAsync(AppUser user, IEnumerable<string> roles);
    Task DeleteUserRolesAsync(string userId, CancellationToken cancellationToken);
    /// <summary>Applies only the role differences, atomically on relational providers (roles kept are never removed).</summary>
    Task<IdentityResult> ReplaceUserRolesAsync(AppUser user, IEnumerable<string> roles, CancellationToken cancellationToken);
    /// <summary>
    /// Active (not disabled) accounts that effectively hold the role: the account type and demo flag must be compatible
    /// with it (see <see cref="PlatformRole.IsCompatibleWith"/>). Counted by the database.
    /// </summary>
    Task<int> CountActiveUsersInRoleAsync(PlatformRole role, CancellationToken cancellationToken);
    /// <summary>
    /// Runs a change that could reduce the number of active holders of a role while holding an exclusive lock on the
    /// role, so concurrent changes run one after another and each sees the previous one's result. The change commits
    /// only when <paramref name="succeeded"/> returns true. Used by the last-SuperAdmin guard.
    /// </summary>
    Task<TResult> RunExclusiveForRoleAsync<TResult>(PlatformRole role, Func<Task<TResult>> change, Func<TResult, bool> succeeded, CancellationToken cancellationToken);
    Task<IdentityResult> SetLockoutEndDateAsync(AppUser user, DateTimeOffset? lockoutEnd);
    Task UpdateUserProfileAsync(string userId, string firstName, string lastName);

    // Email confirmation
    Task<string> GenerateEmailConfirmationTokenAsync(AppUser user);
    Task<IdentityResult> ConfirmEmailAsync(AppUser user, string code);

    // Password reset
    Task<string> GeneratePasswordResetTokenAsync(AppUser user);
    Task<IdentityResult> ResetPasswordAsync(AppUser user, string code, string newPassword);

    // Password change
    /// <summary>Revokes every active refresh token of the user (used after password change/reset).</summary>
    Task<int> RevokeAllRefreshTokensAsync(string userId, CancellationToken cancellationToken);
    Task<IdentityResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword);

    /// <summary>User with candidate profile (admin user details).</summary>
    Task<AppUser?> FindByIdWithProfileAsync(string userId, CancellationToken cancellationToken);
}