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

    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitSection = configuration.GetSection("RateLimiting");

        var globalLimit = rateLimitSection.GetValue("Global:PermitLimit", 1000);
        var globalWindowMinutes = rateLimitSection.GetValue("Global:WindowMinutes", 1);

        var authNLimit = rateLimitSection.GetValue("Authentication:PermitLimit", 10);
        var authNWindowMinutes = rateLimitSection.GetValue("Authentication:WindowMinutes", 1);
        var authNQueueLimit = rateLimitSection.GetValue("Authentication:QueueLimit", 0);

        var authenticatedLimit = rateLimitSection.GetValue("Authenticated:PermitLimit", 100);
        var authenticatedWindowMinutes = rateLimitSection.GetValue("Authenticated:WindowMinutes", 1);
        var authenticatedQueueLimit = rateLimitSection.GetValue("Authenticated:QueueLimit", 2);

        var anonymousLimit = rateLimitSection.GetValue("Anonymous:PermitLimit", 30);
        var anonymousWindowMinutes = rateLimitSection.GetValue("Anonymous:WindowMinutes", 1);
        var anonymousQueueLimit = rateLimitSection.GetValue("Anonymous:QueueLimit", 2);

        var readOnlyLimit = rateLimitSection.GetValue("ReadOnly:PermitLimit", 200);
        var readOnlyWindowMinutes = rateLimitSection.GetValue("ReadOnly:WindowMinutes", 1);
        var readOnlyQueueLimit = rateLimitSection.GetValue("ReadOnly:QueueLimit", 5);

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

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isAuthenticated ? authenticatedLimit : anonymousLimit,
                        Window = TimeSpan.FromMinutes(isAuthenticated ? authenticatedWindowMinutes : anonymousWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = isAuthenticated ? authenticatedQueueLimit : anonymousQueueLimit
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
