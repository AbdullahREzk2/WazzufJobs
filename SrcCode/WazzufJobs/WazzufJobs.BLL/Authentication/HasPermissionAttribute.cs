using Microsoft.AspNetCore.Authorization;

namespace WazzufJobs.BLL.Authentication;
public class HasPermissionAttribute(string permission) : AuthorizeAttribute(permission)
{
}
