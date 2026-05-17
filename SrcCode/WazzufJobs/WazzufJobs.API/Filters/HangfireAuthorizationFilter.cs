using Hangfire.Dashboard;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var user = httpContext.Request.Query["u"];
        var pass = httpContext.Request.Query["p"];

        return user == "admin" && pass == "2003";
    }
}