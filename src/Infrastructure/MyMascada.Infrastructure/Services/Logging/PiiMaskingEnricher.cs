using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace MyMascada.Infrastructure.Services.Logging;

/// <summary>
/// Serilog enricher that masks PII in log event properties.
/// Automatically detects and masks sensitive data patterns.
/// </summary>
public class PiiMaskingEnricher : ILogEventEnricher
{
    private readonly IPiiMaskingService _piiMaskingService;

    // Properties that should always be masked
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email",
        "EmailAddress",
        "UserEmail",
        "Phone",
        "PhoneNumber",
        "Mobile",
        "AccountNumber",
        "BankAccount",
        "CardNumber",
        "CreditCard",
        "Password",
        "Secret",
        "Token",
        "ApiKey",
        "FirstName",
        "LastName",
        "FullName",
        "Name",
        "Address",
        "Description",
        "TransactionDescription"
    };

    public PiiMaskingEnricher(IPiiMaskingService piiMaskingService)
    {
        _piiMaskingService = piiMaskingService;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToUpdate = new List<(string Name, LogEventProperty NewProperty)>();

        foreach (var property in logEvent.Properties)
        {
            if (ShouldMaskProperty(property.Key))
            {
                var maskedValue = MaskPropertyValue(property.Value);
                if (maskedValue != null)
                {
                    var newProperty = propertyFactory.CreateProperty(property.Key, maskedValue);
                    propertiesToUpdate.Add((property.Key, newProperty));
                }
            }
        }

        // Apply masked properties
        foreach (var (name, newProperty) in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(newProperty);
        }
    }

    private static bool ShouldMaskProperty(string propertyName)
    {
        // Check exact match
        if (SensitivePropertyNames.Contains(propertyName))
            return true;

        // Check if property name contains sensitive keywords
        return SensitivePropertyNames.Any(sensitive =>
            propertyName.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private string? MaskPropertyValue(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue scalar when scalar.Value is string strValue => _piiMaskingService.MaskPii(strValue),
            ScalarValue scalar => scalar.Value?.ToString(),
            _ => "[COMPLEX_VALUE_REDACTED]"
        };
    }
}

/// <summary>
/// Extension methods for configuring PII masking with Serilog.
/// </summary>
public static class PiiMaskingSerilogExtensions
{
    /// <summary>
    /// Adds PII masking enricher to the Serilog configuration.
    /// </summary>
    public static LoggerConfiguration WithPiiMasking(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        IPiiMaskingService piiMaskingService)
    {
        return enrichmentConfiguration.With(new PiiMaskingEnricher(piiMaskingService));
    }
}
