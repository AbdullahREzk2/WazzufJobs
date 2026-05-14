using Microsoft.AspNetCore.Identity;
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
    Task<IList<string>> GetUserPermissionsAsync(IList<string> userRoles, CancellationToken cancellationToken);
    Task<IList<string>> GetRolesAsync(AppUser user);
    Task<List<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<List<UserWithRoles>> GetUsersWithRolesAsync(CancellationToken cancellationToken);
    Task<AppUser?> FindByIdWithCVAsync(string userId, CancellationToken cancellationToken);

    // User lifecycle
    Task<IdentityResult> CreateAsync(AppUser user, string password);
    Task<IdentityResult> UpdateAsync(AppUser user);
    Task<IdentityResult> AddToRoleAsync(AppUser user, string role);
    Task<IdentityResult> AddToRolesAsync(AppUser user, IEnumerable<string> roles);
    Task DeleteUserRolesAsync(string userId, CancellationToken cancellationToken);
    Task<IdentityResult> SetLockoutEndDateAsync(AppUser user, DateTimeOffset? lockoutEnd);
    Task UpdateUserProfileAsync(string userId, string firstName, string lastName);

    // Email confirmation
    Task<string> GenerateEmailConfirmationTokenAsync(AppUser user);
    Task<IdentityResult> ConfirmEmailAsync(AppUser user, string code);

    // Password reset
    Task<string> GeneratePasswordResetTokenAsync(AppUser user);
    Task<IdentityResult> ResetPasswordAsync(AppUser user, string code, string newPassword);

    // Password change
    Task<IdentityResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword);
}