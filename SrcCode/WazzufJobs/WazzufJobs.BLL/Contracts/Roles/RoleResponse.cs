namespace WazzufJobs.BLL.Contracts.Roles;

/// <param name="isDeleted">Always false: roles are fixed (kept for compatibility).</param>
public record RoleResponse(
    string Id,
    string Name,
    bool isDeleted,
    string Description,
    string AccountType
    );
