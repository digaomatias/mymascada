using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting.
/// </summary>
public static class RateLimitingServiceExtensions
{
    /// <summary>
    /// Rate limiting policy names for use with [EnableRateLimiting] attribute.
    /// </summary>
    public static class Policies
    {
        /// <summary>
        /// Strict rate limiting for authentication endpoints (login, register, password reset).
        /// </summary>
        public const string Authentication = "authentication";

        /// <summary>
        /// Standard rate limiting for general API endpoints.
        /// </summary>
        public const string Standard = "standard";

        /// <summary>
        /// Relaxed rate limiting for read-only endpoints.
        /// </summary>
        public const string ReadOnly = "readonly";
    }

    private static class ConfigKeys
    {
        public const string Section = "RateLimiting";

        public const string GlobalPermitLimit = "Global:PermitLimit";
        public const string GlobalWindowMinutes = "Global:WindowMinutes";

        public const string AuthenticationPermitLimit = "Authentication:PermitLimit";
        public const string AuthenticationWindowMinutes = "Authentication:WindowMinutes";
        public const string AuthenticationQueueLimit = "Authentication:QueueLimit";

        public const string AuthenticatedPermitLimit = "Authenticated:PermitLimit";
        public const string AuthenticatedWindowMinutes = "Authenticated:WindowMinutes";
        public const string AuthenticatedQueueLimit = "Authenticated:QueueLimit";

        public const string AnonymousPermitLimit = "Anonymous:PermitLimit";
        public const string AnonymousWindowMinutes = "Anonymous:WindowMinutes";
        public const string AnonymousQueueLimit = "Anonymous:QueueLimit";

        public const string ReadOnlyPermitLimit = "ReadOnly:PermitLimit";
        public const string ReadOnlyWindowMinutes = "ReadOnly:WindowMinutes";
        public const string ReadOnlyQueueLimit = "ReadOnly:QueueLimit";
    }

    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitSection = configuration.GetSection(ConfigKeys.Section);

        var globalLimit = rateLimitSection.GetValue(ConfigKeys.GlobalPermitLimit, 1000);
        var globalWindowMinutes = rateLimitSection.GetValue(ConfigKeys.GlobalWindowMinutes, 1);

        var authNLimit = rateLimitSection.GetValue(ConfigKeys.AuthenticationPermitLimit, 10);
        var authNWindowMinutes = rateLimitSection.GetValue(ConfigKeys.AuthenticationWindowMinutes, 1);
        var authNQueueLimit = rateLimitSection.GetValue(ConfigKeys.AuthenticationQueueLimit, 0);

        var authenticatedLimit = rateLimitSection.GetValue(ConfigKeys.AuthenticatedPermitLimit, 100);
        var authenticatedWindowMinutes = rateLimitSection.GetValue(ConfigKeys.AuthenticatedWindowMinutes, 1);
        var authenticatedQueueLimit = rateLimitSection.GetValue(ConfigKeys.AuthenticatedQueueLimit, 2);

        var anonymousLimit = rateLimitSection.GetValue(ConfigKeys.AnonymousPermitLimit, 30);
        var anonymousWindowMinutes = rateLimitSection.GetValue(ConfigKeys.AnonymousWindowMinutes, 1);
        var anonymousQueueLimit = rateLimitSection.GetValue(ConfigKeys.AnonymousQueueLimit, 2);

        var readOnlyLimit = rateLimitSection.GetValue(ConfigKeys.ReadOnlyPermitLimit, 200);
        var readOnlyWindowMinutes = rateLimitSection.GetValue(ConfigKeys.ReadOnlyWindowMinutes, 1);
        var readOnlyQueueLimit = rateLimitSection.GetValue(ConfigKeys.ReadOnlyQueueLimit, 5);

        services.AddRateLimiter(options =>
        {
            // Global limiter as fallback
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: remoteIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalLimit,
                        Window = TimeSpan.FromMinutes(globalWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Authentication endpoints - strict limits to prevent brute force
            options.AddPolicy(Policies.Authentication, context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: remoteIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authNLimit,
                        Window = TimeSpan.FromMinutes(authNWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = authNQueueLimit
                    });
            });

            // Standard API endpoints - moderate limits per user
            options.AddPolicy(Policies.Standard, context =>
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var partitionKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var isAuthenticated = userId != null;

                var permitLimit = isAuthenticated ? authenticatedLimit : anonymousLimit;
                var windowMinutes = isAuthenticated ? authenticatedWindowMinutes : anonymousWindowMinutes;
                var queueLimit = isAuthenticated ? authenticatedQueueLimit : anonymousQueueLimit;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(windowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = queueLimit
                    });
            });

            // Read-only endpoints - relaxed limits
            options.AddPolicy(Policies.ReadOnly, context =>
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var partitionKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = readOnlyLimit,
                        Window = TimeSpan.FromMinutes(readOnlyWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = readOnlyQueueLimit
                    });
            });

            // Custom rejection response
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue.TotalSeconds
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString("F0");

                var response = new
                {
                    error = "Too many requests",
                    message = "You have exceeded the rate limit. Please try again later.",
                    retryAfterSeconds = (int)retryAfter
                };

                await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);

                var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var path = context.HttpContext.Request.Path;
                logger?.LogWarning("Rate limit exceeded for IP {RemoteIp} on {Path}", remoteIp, path);
            };
        });

        return services;
    }
}
