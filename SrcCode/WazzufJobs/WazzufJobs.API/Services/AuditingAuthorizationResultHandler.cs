using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Routing;
using WazzufJobs.BLL.Audit;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.Enums;

namespace WazzufJobs.API.Services;

/// <summary>
/// Audits authorization denials (Phase 02, S5): a signed-in caller refused by a permission policy produces one
/// <c>authz.denied</c> entry (route template and required permissions, never the concrete path or query). Unauthenticated
/// requests (401) are not audited. The response is exactly the default one; an audit failure never changes it.
/// </summary>
public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            var permissions = policy.Requirements.OfType<PermissionRequirment>().Select(r => r.Permission).ToList();
            await context.RequestServices.GetRequiredService<IAuditLog>().WriteAsync(
                AuditEvent.Denied(AuditActions.AuthorizationDenied)
                    .On(AuditTargets.Endpoint, null)
                    .With(("method", context.Request.Method), ("route", route), ("permissions", permissions)),
                CancellationToken.None);
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
