using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MyMascada.Infrastructure.Services.Logging;

/// <summary>
/// Adapter that implements the application logging interface using Serilog.
/// This provides a clean abstraction layer between the application and logging infrastructure.
/// </summary>
public class ApplicationLoggerAdapter<T> : IApplicationLogger<T>
{
    private readonly ILogger<T> _logger;
    private readonly Serilog.ILogger _serilogLogger;

    public ApplicationLoggerAdapter(ILogger<T> logger)
    {
        _logger = logger;
        _serilogLogger = Log.ForContext<T>();
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogInformation(string message, object properties)
    {
        _serilogLogger.Information(message, properties);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogWarning(Exception exception, string message, params object[] args)
    {
        _logger.LogWarning(exception, message, args);
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    public void LogError(Exception exception, string message, object properties)
    {
        _serilogLogger.Error(exception, message, properties);
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        _logger.LogCritical(exception, message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    public void LogAudit(string operation, object properties)
    {
        _serilogLogger
            .ForContext("EventType", "Audit")
            .ForContext("Operation", operation)
            .Information("Audit: {Operation} {@Properties}", operation, properties);
    }

    public void LogPerformance(string operation, TimeSpan duration, object? properties = null)
    {
        _serilogLogger
            .ForContext("EventType", "Performance")
            .ForContext("Operation", operation)
            .ForContext("Duration", duration.TotalMilliseconds)
            .Information("Performance: {Operation} completed in {Duration}ms {@Properties}", 
                operation, duration.TotalMilliseconds, properties);
    }

    public void LogSecurity(string event_, object properties)
    {
        _serilogLogger
            .ForContext("EventType", "Security")
            .ForContext("SecurityEvent", event_)
            .Warning("Security: {SecurityEvent} {@Properties}", event_, properties);
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }
}

/// <summary>
/// Specialized audit logger implementation for financial compliance requirements.
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly Serilog.ILogger _auditLogger;

    public AuditLogger()
    {
        // Create a specialized logger for audit events
        _auditLogger = Log.ForContext("SourceContext", "AuditLogger")
                          .ForContext("EventType", "Audit");
    }

    public async Task LogTransactionOperationAsync(
        string operation,
        Guid userId,
        int? transactionId,
        decimal? amount,
        string? description,
        object? additionalData = null)
    {
        await Task.Run(() =>
        {
            _auditLogger
                .ForContext("Operation", operation)
                .ForContext("UserId", userId)
                .ForContext("TransactionId", transactionId)
                .ForContext("Amount", amount)
                .ForContext("Description", description)
                .Information("Transaction Audit: {Operation} by {UserId} - Transaction {TransactionId} Amount {Amount} {@AdditionalData}",
                    operation, userId, transactionId, amount, additionalData);
        });
    }

    public async Task LogAuthenticationEventAsync(
        string event_,
        Guid? userId,
        string? userEmail,
        string? ipAddress,
        bool success,
        string? failureReason = null)
    {
        await Task.Run(() =>
        {
            var logLevel = success ? Serilog.Events.LogEventLevel.Information : Serilog.Events.LogEventLevel.Warning;
            
            _auditLogger
                .ForContext("AuthEvent", event_)
                .ForContext("UserId", userId)
                .ForContext("UserEmail", userEmail)
                .ForContext("IpAddress", ipAddress)
                .ForContext("Success", success)
                .ForContext("FailureReason", failureReason)
                .Write(logLevel, "Authentication Audit: {AuthEvent} for {UserEmail} from {IpAddress} - Success: {Success} {FailureReason}",
                    event_, userEmail, ipAddress, success, failureReason);
        });
    }

    public async Task LogDataAccessAsync(
        string operation,
        Guid userId,
        string entityType,
        string? entityId,
        object? queryParameters = null)
    {
        await Task.Run(() =>
        {
            _auditLogger
                .ForContext("Operation", operation)
                .ForContext("UserId", userId)
                .ForContext("EntityType", entityType)
                .ForContext("EntityId", entityId)
                .Information("Data Access Audit: {Operation} on {EntityType} {EntityId} by {UserId} {@QueryParameters}",
                    operation, entityType, entityId, userId, queryParameters);
        });
    }

    public async Task LogConfigurationChangeAsync(
        string setting,
        string? oldValue,
        string? newValue,
        Guid? userId,
        string? source)
    {
        await Task.Run(() =>
        {
            _auditLogger
                .ForContext("Setting", setting)
                .ForContext("OldValue", oldValue)
                .ForContext("NewValue", newValue)
                .ForContext("UserId", userId)
                .ForContext("Source", source)
                .Warning("Configuration Audit: {Setting} changed from {OldValue} to {NewValue} by {UserId} via {Source}",
                    setting, oldValue, newValue, userId, source);
        });
    }
}