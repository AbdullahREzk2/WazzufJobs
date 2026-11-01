namespace WazzufJobs.BLL.Contracts.Roles;

/// <param name="isDeleted">Always false: roles are fixed (kept for compatibility).</param>
public record RoleDetailResponse(
    string Id,
    string Name,
    bool isDeleted,
    IEnumerable<string> Permissions,
    string Description,
    string AccountType
    );
