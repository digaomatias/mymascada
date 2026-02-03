using ProviderBankTransactionDto = MyMascada.Application.Features.BankConnections.DTOs.BankTransactionDto;
using ReconciliationBankTransactionDto = MyMascada.Application.Features.Reconciliation.DTOs.BankTransactionDto;

namespace MyMascada.Application.Features.Reconciliation.Services;

/// <summary>
/// Maps bank provider transaction DTOs to reconciliation transaction DTOs
/// </summary>
public static class AkahuTransactionMapper
{
    /// <summary>
    /// Maps a collection of provider bank transactions to reconciliation format
    /// </summary>
    public static IEnumerable<ReconciliationBankTransactionDto> MapToReconciliationFormat(
        IEnumerable<ProviderBankTransactionDto> providerTransactions)
    {
        return providerTransactions.Select(MapSingle);
    }

    /// <summary>
    /// Maps a single provider bank transaction to reconciliation format
    /// </summary>
    public static ReconciliationBankTransactionDto MapSingle(ProviderBankTransactionDto source)
    {
        return new ReconciliationBankTransactionDto
        {
            BankTransactionId = source.ExternalId,
            TransactionDate = source.Date,
            Amount = source.Amount,
            Description = !string.IsNullOrEmpty(source.MerchantName)
                ? source.MerchantName
                : source.Description,
            BankCategory = source.Category,
            Reference = source.Reference
        };
    }

    /// <summary>
    /// Creates JSON reference data for storing in ReconciliationItem.BankReferenceData
    /// Includes full Akahu transaction details for later import
    /// Property names match BankTransactionDto for deserialization
    /// </summary>
    public static Dictionary<string, object?> CreateBankReferenceData(ProviderBankTransactionDto source)
    {
        // Use the display description (merchant name if available)
        var displayDescription = !string.IsNullOrEmpty(source.MerchantName)
            ? source.MerchantName
            : source.Description;

        var referenceData = new Dictionary<string, object?>
        {
            // Match BankTransactionDto property names for proper deserialization
            ["BankTransactionId"] = source.ExternalId,
            ["TransactionDate"] = source.Date.ToString("O"),
            ["Amount"] = source.Amount,
            ["Description"] = displayDescription,
            ["Reference"] = source.Reference,
            ["BankCategory"] = source.Category,
            // Also store original values for import
            ["OriginalDescription"] = source.Description,
            ["MerchantName"] = source.MerchantName
        };

        // Include any additional metadata from the provider
        if (source.Metadata != null)
        {
            referenceData["Metadata"] = source.Metadata;
        }

        return referenceData;
    }
}
