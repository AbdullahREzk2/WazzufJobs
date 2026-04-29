using Microsoft.AspNetCore.Authorization;

namespace WazzufJobs.BLL.Authentication;
public class PermissionRequirment(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;

}
