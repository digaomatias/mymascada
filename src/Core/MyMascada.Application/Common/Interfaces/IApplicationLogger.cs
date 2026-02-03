using Microsoft.Extensions.Logging;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Application-specific logging interface that follows Clean Architecture principles.
/// Provides structured logging capabilities without coupling to specific logging implementations.
/// </summary>
public interface IApplicationLogger<T>
{
    /// <summary>
    /// Logs information level messages with structured data
    /// </summary>
    void LogInformation(string message, params object[] args);
    
    /// <summary>
    /// Logs information with additional properties for structured logging
    /// </summary>
    void LogInformation(string message, object properties);
    
    /// <summary>
    /// Logs warning level messages
    /// </summary>
    void LogWarning(string message, params object[] args);
    
    /// <summary>
    /// Logs warning with exception context
    /// </summary>
    void LogWarning(Exception exception, string message, params object[] args);
    
    /// <summary>
    /// Logs error level messages with exception details
    /// </summary>
    void LogError(Exception exception, string message, params object[] args);
    
    /// <summary>
    /// Logs error with additional structured properties
    /// </summary>
    void LogError(Exception exception, string message, object properties);
    
    /// <summary>
    /// Logs critical application errors
    /// </summary>
    void LogCritical(Exception exception, string message, params object[] args);
    
    /// <summary>
    /// Logs debug information (only in development)
    /// </summary>
    void LogDebug(string message, params object[] args);
    
    /// <summary>
    /// Logs audit trail information for financial operations
    /// </summary>
    void LogAudit(string operation, object properties);
    
    /// <summary>
    /// Logs performance metrics for monitoring
    /// </summary>
    void LogPerformance(string operation, TimeSpan duration, object? properties = null);
    
    /// <summary>
    /// Logs security-related events
    /// </summary>
    void LogSecurity(string event_, object properties);
    
    /// <summary>
    /// Creates a scoped logger with additional context properties
    /// </summary>
    IDisposable BeginScope<TState>(TState state) where TState : notnull;
}

/// <summary>
/// Audit logging interface for financial operations requiring compliance trails
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs financial transaction operations for audit trails
    /// </summary>
    Task LogTransactionOperationAsync(
        string operation,
        Guid userId,
        int? transactionId,
        decimal? amount,
        string? description,
        object? additionalData = null);
    
    /// <summary>
    /// Logs user authentication and authorization events
    /// </summary>
    Task LogAuthenticationEventAsync(
        string event_,
        Guid? userId,
        string? userEmail,
        string? ipAddress,
        bool success,
        string? failureReason = null);
    
    /// <summary>
    /// Logs data access operations for sensitive financial data
    /// </summary>
    Task LogDataAccessAsync(
        string operation,
        Guid userId,
        string entityType,
        string? entityId,
        object? queryParameters = null);
    
    /// <summary>
    /// Logs system configuration changes
    /// </summary>
    Task LogConfigurationChangeAsync(
        string setting,
        string? oldValue,
        string? newValue,
        Guid? userId,
        string? source);
}