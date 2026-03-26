using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MyMascada.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior that provides comprehensive logging for all commands and queries.
/// Logs request start, completion, duration, and any exceptions that occur.
/// </summary>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationLogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(IApplicationLogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        // Log request start with structured data
        _logger.LogInformation("Processing {RequestName} with RequestId {RequestId}", requestName, requestId);

        try
        {
            // Execute the actual request handler
            var response = await next();
            
            stopwatch.Stop();
            
            // Log successful completion with performance metrics
            _logger.LogPerformance(
                $"{requestName} completed successfully",
                stopwatch.Elapsed,
                new { RequestId = requestId, RequestName = requestName });

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log error with full context
            _logger.LogError(ex, 
                "Error processing {RequestName} with RequestId {RequestId}. Duration: {Duration}ms",
                requestName, requestId, stopwatch.ElapsedMilliseconds);
            
            throw; // Re-throw to maintain exception flow
        }
    }
}

/// <summary>
/// Specialized logging behavior for commands that modify financial data.
/// Provides enhanced audit logging for compliance requirements.
/// </summary>
public class AuditLoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogger _auditLogger;
    private readonly IApplicationLogger<AuditLoggingBehaviour<TRequest, TResponse>> _logger;

    public AuditLoggingBehaviour(
        IAuditLogger auditLogger,
        IApplicationLogger<AuditLoggingBehaviour<TRequest, TResponse>> logger)
    {
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        // Only audit commands (operations that modify data)
        if (!IsAuditableCommand(requestName))
        {
            return await next();
        }

        try
        {
            var response = await next();
            
            // Log audit trail for successful financial operations
            await LogAuditTrailAsync(request, requestName, response, success: true);
            
            return response;
        }
        catch (Exception ex)
        {
            // Still log audit trail for failed operations
            await LogAuditTrailAsync(request, requestName, default(TResponse), success: false, ex);
            throw;
        }
    }

    private enum AuditCategory
    {
        Transaction,
        Authentication,
        DataAccess,
        ConfigurationChange
    }

    private static readonly HashSet<string> TransactionCommands =
    [
        "CreateTransactionCommand",
        "UpdateTransactionCommand",
        "DeleteTransactionCommand",
        "BulkDeleteTransactionsCommand",
        "ImportCsvTransactionsCommand",
        "ImportCsvWithMappingsCommand",
        "ImportOfxFileCommand",
        "ExecuteImportCommand",
        "CreateTransferCommand",
        "ReverseTransferCommand",
        "CreateMissingTransferCommand",
        "LinkTransactionsAsTransferCommand",
        "BulkAssignCategoryCommand",
        "CreateAccountCommand",
        "UpdateAccountCommand",
        "DeleteAccountCommand"
    ];

    private static readonly HashSet<string> AuthenticationCommands =
    [
        "RegisterCommand",
        "ChangePasswordCommand",
        "ResetPasswordCommand",
        "ForgotPasswordCommand",
        "ConfirmEmailCommand"
    ];

    private static readonly HashSet<string> DataAccessCommands =
    [
        "DeleteUserAccountCommand",
        "InitiateAkahuConnectionCommand",
        "CompleteAkahuConnectionCommand",
        "DisconnectBankConnectionCommand",
        "SaveAkahuCredentialsCommand",
        "SyncBankConnectionCommand",
        "SyncAllConnectionsCommand",
        "CreateAccountShareCommand",
        "RevokeAccountShareCommand",
        "AcceptAccountShareCommand",
        "AcceptAccountShareByIdCommand"
    ];

    private static readonly HashSet<string> ConfigurationChangeCommands =
    [
        "UpdateNotificationPreferencesCommand",
        "CompleteOnboardingCommand",
        "GenerateInvitationCommand",
        "CreateRuleCommand",
        "CreateBankCategoryMappingCommand",
        "UpdateBankCategoryMappingCommand",
        "DeleteBankCategoryMappingCommand",
        "SetBankCategoryExclusionCommand"
    ];

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> PropertyCache = new();

    private static AuditCategory? GetAuditCategory(string requestName)
    {
        if (TransactionCommands.Contains(requestName))
            return AuditCategory.Transaction;
        if (AuthenticationCommands.Contains(requestName))
            return AuditCategory.Authentication;
        if (DataAccessCommands.Contains(requestName))
            return AuditCategory.DataAccess;
        if (ConfigurationChangeCommands.Contains(requestName))
            return AuditCategory.ConfigurationChange;
        return null;
    }

    private static bool IsAuditableCommand(string requestName)
    {
        return GetAuditCategory(requestName) != null;
    }

    private async Task LogAuditTrailAsync(TRequest request, string requestName, TResponse? response, bool success, Exception? exception = null)
    {
        try
        {
            var category = GetAuditCategory(requestName);
            var userId = ExtractUserId(request);

            switch (category)
            {
                case AuditCategory.Authentication:
                    var email = ExtractStringProperty(request, "Email");
                    var ipAddress = ExtractStringProperty(request, "IpAddress");
                    await _auditLogger.LogAuthenticationEventAsync(
                        event_: requestName,
                        userId: userId != Guid.Empty ? userId : null,
                        userEmail: email,
                        ipAddress: ipAddress,
                        success: success,
                        failureReason: exception?.Message);
                    break;

                case AuditCategory.DataAccess:
                    var entityId = ExtractStringProperty(request, "Id")
                                   ?? ExtractStringProperty(request, "ConnectionId")
                                   ?? ExtractStringProperty(request, "ShareId");
                    await _auditLogger.LogDataAccessAsync(
                        operation: requestName,
                        userId: userId,
                        entityType: requestName.Replace("Command", ""),
                        entityId: entityId,
                        queryParameters: SerializeForAudit(request));
                    break;

                case AuditCategory.ConfigurationChange:
                    await _auditLogger.LogConfigurationChangeAsync(
                        setting: requestName,
                        oldValue: null,
                        newValue: SerializeForAudit(request)?.ToString(),
                        userId: userId != Guid.Empty ? userId : null,
                        source: "MediatR");
                    break;

                case AuditCategory.Transaction:
                default:
                    var auditData = ExtractAuditData(request, response);
                    await _auditLogger.LogTransactionOperationAsync(
                        operation: requestName,
                        userId: userId,
                        transactionId: auditData.TransactionId,
                        amount: auditData.Amount,
                        description: auditData.Description,
                        additionalData: new
                        {
                            Success = success,
                            ErrorMessage = exception?.Message,
                            Timestamp = DateTime.UtcNow,
                            RequestData = SerializeForAudit(request),
                            ResponseData = success ? SerializeForAudit(response) : null
                        });
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log audit logging failure but don't throw
            _logger.LogError(ex, "Failed to log audit trail for {RequestName}", requestName);
        }
    }

    private static Guid ExtractUserId(TRequest request)
    {
        // Use reflection to find UserId property in request
        var userIdProperty = request.GetType().GetProperty("UserId");
        if (userIdProperty?.GetValue(request) is Guid userId)
        {
            return userId;
        }

        return Guid.Empty; // Should not happen in properly designed commands
    }

    private static string? ExtractStringProperty(TRequest request, string propertyName)
    {
        var property = PropertyCache.GetOrAdd(
            (request.GetType(), propertyName),
            key => key.Item1.GetProperty(key.Item2));
        return property?.GetValue(request)?.ToString();
    }

    private static (int? TransactionId, decimal? Amount, string? Description) ExtractAuditData(TRequest request, TResponse? response)
    {
        int? transactionId = null;
        decimal? amount = null;
        string? description = null;

        // Extract transaction ID from request or response
        var requestTransactionId = request.GetType().GetProperty("TransactionId")?.GetValue(request) as int?;
        var requestId = request.GetType().GetProperty("Id")?.GetValue(request) as int?;

        transactionId = requestTransactionId ?? requestId;

        // Extract amount from request
        var amountProperty = request.GetType().GetProperty("Amount");
        if (amountProperty?.GetValue(request) is decimal amt)
        {
            amount = amt;
        }

        // Extract description from request - mask PII before logging
        var descriptionProperty = request.GetType().GetProperty("Description");
        if (descriptionProperty?.GetValue(request) is string desc)
        {
            // Mask any PII patterns in the description (emails, phone numbers, etc.)
            description = MaskDescriptionPii(desc);
        }

        return (transactionId, amount, description);
    }

    /// <summary>
    /// Basic PII masking for audit log descriptions.
    /// Masks common patterns like emails and phone numbers.
    /// </summary>
    private static string MaskDescriptionPii(string description)
    {
        if (string.IsNullOrEmpty(description))
            return description;

        // Mask email patterns
        var emailPattern = new System.Text.RegularExpressions.Regex(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        description = emailPattern.Replace(description, match =>
        {
            var email = match.Value;
            var atIndex = email.IndexOf('@');
            if (atIndex <= 2) return "***@" + email[(atIndex + 1)..];
            return email[..2] + "***@" + email[(atIndex + 1)..];
        });

        // Mask phone patterns (basic pattern)
        var phonePattern = new System.Text.RegularExpressions.Regex(
            @"(?:\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}");
        description = phonePattern.Replace(description, "***[PHONE]***");

        return description;
    }

    private static object? SerializeForAudit(object? obj)
    {
        if (obj == null) return null;
        
        // For audit purposes, we create a simplified representation
        // avoiding circular references and sensitive data
        return new
        {
            Type = obj.GetType().Name,
            // Add other relevant properties as needed
            Timestamp = DateTime.UtcNow
        };
    }
}