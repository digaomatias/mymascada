using System.Security.Claims;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Middleware;

public class UsageLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IFeatureFlags _featureFlags;

    // Paths where we enforce usage limits
    private static readonly HashSet<string> AccountCreationPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/accounts"
    };

    private static readonly HashSet<string> TransactionCreationPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/transactions"
    };

    public UsageLimitMiddleware(RequestDelegate next, IFeatureFlags featureFlags)
    {
        _next = next;
        _featureFlags = featureFlags;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip entirely when billing is disabled — all features are free
        if (!_featureFlags.StripeBilling)
        {
            await _next(context);
            return;
        }

        // Only check POST requests (creation endpoints)
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Only check authenticated users
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        // Check account creation limit
        if (AccountCreationPaths.Contains(path))
        {
            var billingService = context.RequestServices.GetRequiredService<IBillingService>();
            var status = await billingService.GetBillingStatusAsync(userId);

            if (status != null && status.MaxAccounts > 0 && status.CurrentAccountCount >= status.MaxAccounts)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Account limit reached. Please upgrade your plan.",
                    code = "ACCOUNT_LIMIT_REACHED"
                });
                return;
            }
        }

        // Check transaction creation limit
        if (TransactionCreationPaths.Contains(path))
        {
            var billingService = context.RequestServices.GetRequiredService<IBillingService>();
            var status = await billingService.GetBillingStatusAsync(userId);

            if (status != null && status.MaxTransactionsPerMonth > 0 &&
                status.CurrentMonthTransactionCount >= status.MaxTransactionsPerMonth)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Monthly transaction limit reached. Please upgrade your plan.",
                    code = "TRANSACTION_LIMIT_REACHED"
                });
                return;
            }
        }

        await _next(context);
    }
}

public static class UsageLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseUsageLimits(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UsageLimitMiddleware>();
    }
}
