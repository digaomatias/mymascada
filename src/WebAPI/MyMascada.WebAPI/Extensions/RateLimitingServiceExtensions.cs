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
        /// 5 requests per minute per IP.
        /// </summary>
        public const string Authentication = "authentication";

        /// <summary>
        /// Standard rate limiting for general API endpoints.
        /// 100 requests per minute per user, 30 requests per minute for anonymous.
        /// </summary>
        public const string Standard = "standard";

        /// <summary>
        /// Relaxed rate limiting for read-only endpoints.
        /// 200 requests per minute per user.
        /// </summary>
        public const string ReadOnly = "readonly";
    }

    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global limiter as fallback - 1000 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: remoteIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1000,
                        Window = TimeSpan.FromMinutes(1),
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
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Standard API endpoints - moderate limits per user
            options.AddPolicy(Policies.Standard, context =>
            {
                // Use user ID if authenticated, otherwise IP address
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var partitionKey = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var limit = userId != null ? 100 : 30; // More generous for authenticated users

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
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
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
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

                // Log the rate limit hit
                var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var path = context.HttpContext.Request.Path;
                logger?.LogWarning("Rate limit exceeded for IP {RemoteIp} on {Path}", remoteIp, path);
            };
        });

        return services;
    }
}
