using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyMascada.WebAPI.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminApiKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Admin-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<AdminApiKeyAttribute>>();

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var potentialApiKey))
        {
            logger.LogWarning("Admin API authentication failed: no API key header present. IP: {RemoteIp}",
                context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedObjectResult(new { message = "API key is required" });
            return;
        }

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var apiKey = configuration["Admin:ApiKey"];

        if (string.IsNullOrEmpty(apiKey) || !FixedTimeEqual(apiKey, potentialApiKey!))
        {
            logger.LogWarning("Admin API authentication failed: invalid API key. IP: {RemoteIp}",
                context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid API key" });
            return;
        }

        await next();
    }

    private static bool FixedTimeEqual(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        if (expectedBytes.Length != actualBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
