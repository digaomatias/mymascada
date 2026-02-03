using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Middleware;

/// <summary>
/// Global exception handling middleware that provides comprehensive error logging
/// and consistent error responses for the financial application.
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = Guid.NewGuid();
        
        // Extract request information for logging
        var requestInfo = new
        {
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserId = context.User?.Identity?.Name,
            CorrelationId = correlationId
        };

        // Determine exception severity and appropriate response
        var (statusCode, logLevel, userMessage) = ClassifyException(exception);

        // Log the exception with appropriate level and structured data
        switch (logLevel)
        {
            case LogLevel.Error:
                _logger.LogError(exception, 
                    "Unhandled exception occurred processing {Method} {Path}. CorrelationId: {CorrelationId}. RequestInfo: {@RequestInfo}",
                    requestInfo.Method, requestInfo.Path, correlationId, requestInfo);
                break;
                
            case LogLevel.Warning:
                _logger.LogWarning(exception,
                    "Business rule violation or validation error for {Method} {Path}. CorrelationId: {CorrelationId}. RequestInfo: {@RequestInfo}",
                    requestInfo.Method, requestInfo.Path, correlationId, requestInfo);
                break;
                
            case LogLevel.Critical:
                _logger.LogCritical(exception,
                    "Critical system error occurred processing {Method} {Path}. CorrelationId: {CorrelationId}. RequestInfo: {@RequestInfo}",
                    requestInfo.Method, requestInfo.Path, correlationId, requestInfo);
                break;
        }

        // Log security events for authentication/authorization failures
        if (IsSecurityException(exception))
        {
            // Get scoped application logger for security logging
            try
            {
                var appLogger = context.RequestServices.GetService<IApplicationLogger<GlobalExceptionHandlingMiddleware>>();
                appLogger?.LogSecurity("SecurityException", new
                {
                    Exception = exception.GetType().Name,
                    Message = exception.Message,
                    RequestInfo = requestInfo
                });
            }
            catch
            {
                // Fallback to standard logging if application logger is not available
                _logger.LogWarning("Security exception occurred but could not access application logger: {Exception} at {Path}", 
                    exception.GetType().Name, requestInfo.Path);
            }
        }

        // Prepare error response
        var errorResponse = CreateErrorResponse(exception, correlationId, userMessage);

        // Set response headers and content
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // Serialize and write response
        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private (HttpStatusCode statusCode, LogLevel logLevel, string userMessage) ClassifyException(Exception exception)
    {
        return exception switch
        {
            // Business rule violations (more specific exceptions first)
            ArgumentNullException _ => (HttpStatusCode.BadRequest, LogLevel.Warning,
                "Required information is missing. Please provide all necessary data."),
            
            ArgumentException _ => (HttpStatusCode.BadRequest, LogLevel.Warning, 
                "Invalid input provided. Please check your request and try again."),
            
            InvalidOperationException _ => (HttpStatusCode.BadRequest, LogLevel.Warning,
                "This operation is not valid in the current state."),
            
            // Authentication and authorization
            UnauthorizedAccessException _ => (HttpStatusCode.Unauthorized, LogLevel.Warning,
                "You are not authorized to perform this action."),
            
            // Not found scenarios
            KeyNotFoundException _ => (HttpStatusCode.NotFound, LogLevel.Warning,
                "The requested resource was not found."),
            
            // Data access issues
            TimeoutException _ => (HttpStatusCode.RequestTimeout, LogLevel.Error,
                "The request timed out. Please try again later."),
            
            // System errors
            OutOfMemoryException _ => (HttpStatusCode.InternalServerError, LogLevel.Critical,
                "System is experiencing high load. Please try again later."),
            
            StackOverflowException _ => (HttpStatusCode.InternalServerError, LogLevel.Critical,
                "System error occurred. Our team has been notified."),
            
            // Default case
            _ => (HttpStatusCode.InternalServerError, LogLevel.Error,
                "An unexpected error occurred. Our team has been notified.")
        };
    }

    private static bool IsSecurityException(Exception exception) =>
        exception is UnauthorizedAccessException ||
        exception.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase);

    private object CreateErrorResponse(Exception exception, Guid correlationId, string userMessage)
    {
        var errorResponse = new
        {
            Error = new
            {
                Message = userMessage,
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                Type = exception.GetType().Name
            }
        };

        // Include stack trace and detailed message only in development
        if (_environment.IsDevelopment())
        {
            return new
            {
                errorResponse.Error.Message,
                errorResponse.Error.CorrelationId,
                errorResponse.Error.Timestamp,
                errorResponse.Error.Type,
                DetailedMessage = exception.Message,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message
            };
        }

        return errorResponse;
    }
}

/// <summary>
/// Extension method to register the global exception handling middleware
/// </summary>
public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}