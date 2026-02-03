using Hangfire.Dashboard;

namespace MyMascada.WebAPI.Services;

/// <summary>
/// Hangfire Dashboard authorization filter
/// In development: allows all access
/// In production: would require authentication (currently disabled for security)
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In development, allow all access to Hangfire dashboard
        // In production, you should implement proper authentication
        var httpContext = context.GetHttpContext();
        
        // For development, allow access from localhost
        if (httpContext.Request.Host.Host == "localhost" || 
            httpContext.Request.Host.Host == "127.0.0.1")
        {
            return true;
        }
        
        // For production, you could check JWT authentication:
        // return httpContext.User.Identity?.IsAuthenticated == true;
        
        return false;
    }
}