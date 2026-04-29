using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.DAL.Persistence.Seeders;

namespace WazzufJobs.DAL.Repository;
public class RoleRepository(RoleManager<ApplicationRole> roleManager, ApplicationDBContext context) : IRoleRepository
{
    private readonly RoleManager<ApplicationRole> _rolemanager = roleManager;
    private readonly ApplicationDBContext _context = context;

    public async Task<IEnumerable<ApplicationRole>> getAllRoles(bool? includeDisabled = false, CancellationToken cancellationToken = default)
    {
        return await _rolemanager.Roles
                    .Where(x => !x.IsDefault && (!x.IsDeleted || (includeDisabled.HasValue && includeDisabled.Value)))
                    .ToListAsync(cancellationToken);
    }
    public async Task<ApplicationRole?> getRoleById(string RoleId)
    {
        return await _rolemanager.FindByIdAsync(RoleId);
    }
    public async Task<IEnumerable<string>> getRolePermissions(ApplicationRole role)
    {
        //var permissions = await _rolemanager.GetClaimsAsync(role);
        //return permissions.Select(x => x.Value);

        return await _context.RoleClaims
            .Where(x => x.RoleId == role.Id && x.ClaimType == Permissions.Type)
            .Select(x => x.ClaimValue!)
            .ToListAsync();
    }
    public async Task<bool> isRoleExist(string Name)
    {
        return await _rolemanager.RoleExistsAsync(Name);
    }
    public async Task<IdentityResult> CreateRole(ApplicationRole role)
    {
        return await _rolemanager.CreateAsync(role);

    }
    public async Task<IdentityResult> setPermissionsForRole(ApplicationRole role, IEnumerable<string> permissions)
    {
        var Permission = permissions.Select(x => new IdentityRoleClaim<string>
        {
            ClaimType = Permissions.Type,
            ClaimValue = x,
            RoleId = role.Id
        });
        await _context.AddRangeAsync(Permission);
        await _context.SaveChangesAsync();

        return IdentityResult.Success;
    }
    public async Task<bool> isRoleNameExist(string RoleId, string Name)
    {
        return await _rolemanager.Roles.AnyAsync(x => x.Name == Name && x.Id != RoleId);
    }
    public Task<IdentityResult> UpdateRole(ApplicationRole role)
    {
        return _rolemanager.UpdateAsync(role);
    }
    public async Task<IdentityResult> removePermissionForRole(string roleId, IEnumerable<string> removedPermissions)
    {
        await _context.RoleClaims
            .Where(x => x.RoleId == roleId && removedPermissions.Contains(x.ClaimValue))
            .ExecuteDeleteAsync();
        return IdentityResult.Success;
    }
    public async Task<bool> ToggleStatus(string roleId)
    {
        var role = await _rolemanager.FindByIdAsync(roleId);

        role!.IsDeleted = !role.IsDeleted;

        await _rolemanager.UpdateAsync(role);

        return true;

    }
    public IExecutionStrategy CreateExecutionStrategy()
    {
        return _context.Database.CreateExecutionStrategy();
    }
    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await _context.Database.BeginTransactionAsync();
    }

}
