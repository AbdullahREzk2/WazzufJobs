using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace WazzufJobs.BLL.Authentication;
public class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private readonly AuthorizationOptions _authorizationoptions = options.Value;
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);

        if (policy is not null)
            return policy;

        var permissionPolicy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirment(policyName))
            .Build();

        _authorizationoptions.AddPolicy(policyName, permissionPolicy);

        return permissionPolicy;
    }

}
