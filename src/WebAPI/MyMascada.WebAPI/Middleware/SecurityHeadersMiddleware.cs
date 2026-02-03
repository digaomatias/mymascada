namespace MyMascada.WebAPI.Middleware;

/// <summary>
/// Middleware to add security headers to all responses.
/// Protects against common web vulnerabilities like XSS, clickjacking, and MIME sniffing.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before the response is sent
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Prevent clickjacking attacks
            if (!headers.ContainsKey("X-Frame-Options"))
            {
                headers["X-Frame-Options"] = "DENY";
            }

            // Prevent MIME type sniffing
            if (!headers.ContainsKey("X-Content-Type-Options"))
            {
                headers["X-Content-Type-Options"] = "nosniff";
            }

            // Enable XSS filter in browsers (legacy, but still useful)
            if (!headers.ContainsKey("X-XSS-Protection"))
            {
                headers["X-XSS-Protection"] = "1; mode=block";
            }

            // Control referrer information
            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            }

            // Restrict browser features/APIs
            if (!headers.ContainsKey("Permissions-Policy"))
            {
                headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            }

            // Content Security Policy - restrict resource loading
            // Note: This is a relatively permissive policy for an API
            // Frontend handles its own CSP via Next.js
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none';";
            }

            // HSTS - Force HTTPS (only in production)
            if (_environment.IsProduction() && !headers.ContainsKey("Strict-Transport-Security"))
            {
                // max-age=31536000 = 1 year, includeSubDomains for all subdomains
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension methods for SecurityHeadersMiddleware registration.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
