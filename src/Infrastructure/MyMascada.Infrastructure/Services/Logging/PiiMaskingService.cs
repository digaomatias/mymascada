using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace MyMascada.Infrastructure.Services.Logging;

/// <summary>
/// Service for masking Personally Identifiable Information (PII) in log messages.
/// Protects sensitive data like emails, phone numbers, and account numbers.
/// </summary>
public interface IPiiMaskingService
{
    /// <summary>
    /// Masks PII in a string value.
    /// </summary>
    string MaskPii(string? value);

    /// <summary>
    /// Masks an email address, showing only first 2 chars and domain.
    /// Example: john.doe@example.com -> jo***@example.com
    /// </summary>
    string MaskEmail(string? email);

    /// <summary>
    /// Masks a phone number, showing only last 4 digits.
    /// Example: +64211234567 -> ***4567
    /// </summary>
    string MaskPhone(string? phone);

    /// <summary>
    /// Masks an account number, showing only last 4 characters.
    /// Example: 1234567890 -> ******7890
    /// </summary>
    string MaskAccountNumber(string? accountNumber);

    /// <summary>
    /// Masks a name, showing only first initial and last name initial.
    /// Example: John Doe -> J*** D***
    /// </summary>
    string MaskName(string? name);

    /// <summary>
    /// Masks transaction description by redacting known PII patterns.
    /// </summary>
    string MaskTransactionDescription(string? description);
}

public partial class PiiMaskingService : IPiiMaskingService
{
    // Pre-compiled regex patterns for better performance
    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?:\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}", RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b\d{8,16}\b", RegexOptions.Compiled)]
    private static partial Regex AccountNumberPattern();

    [GeneratedRegex(@"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13})\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardPattern();

    public string MaskPii(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var result = value;

        // Mask credit card numbers first (most specific)
        result = CreditCardPattern().Replace(result, match => MaskCardNumber(match.Value));

        // Mask email addresses
        result = EmailPattern().Replace(result, match => MaskEmail(match.Value));

        // Mask phone numbers
        result = PhonePattern().Replace(result, match => MaskPhone(match.Value));

        return result;
    }

    public string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return "[REDACTED_EMAIL]";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "[REDACTED_EMAIL]";

        var localPart = email[..atIndex];
        var domainPart = email[(atIndex + 1)..];

        var maskedLocal = localPart.Length <= 2
            ? new string('*', localPart.Length)
            : localPart[..2] + new string('*', Math.Min(localPart.Length - 2, 5));

        return $"{maskedLocal}@{domainPart}";
    }

    public string MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
            return "[REDACTED_PHONE]";

        // Keep only last 4 digits visible
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length <= 4)
            return "***" + digitsOnly;

        return "***" + digitsOnly[^4..];
    }

    public string MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
            return "[REDACTED_ACCOUNT]";

        if (accountNumber.Length <= 4)
            return new string('*', accountNumber.Length);

        return new string('*', accountNumber.Length - 4) + accountNumber[^4..];
    }

    public string MaskName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "[REDACTED_NAME]";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "[REDACTED_NAME]";

        var maskedParts = parts.Select(part =>
            part.Length > 0 ? part[0] + new string('*', Math.Min(part.Length - 1, 3)) : "***"
        );

        return string.Join(" ", maskedParts);
    }

    public string MaskTransactionDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return description ?? string.Empty;

        // Apply general PII masking
        return MaskPii(description);
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 4)
            return "[REDACTED_CARD]";

        return new string('*', digitsOnly.Length - 4) + digitsOnly[^4..];
    }
}

/// <summary>
/// Extension methods for registering PII masking services.
/// </summary>
public static class PiiMaskingServiceExtensions
{
    public static IServiceCollection AddPiiMasking(this IServiceCollection services)
    {
        services.AddSingleton<IPiiMaskingService, PiiMaskingService>();
        return services;
    }
}
