using System.Text.Json;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

/// <summary>
/// Command to import unmatched bank transactions from a reconciliation as new MyMascada transactions.
/// </summary>
public record ImportUnmatchedTransactionsCommand : IRequest<ImportUnmatchedResult>
{
    public Guid UserId { get; init; }
    public int ReconciliationId { get; init; }
    public IEnumerable<int>? ItemIds { get; init; }
    public bool ImportAll { get; init; }
}

public class ImportUnmatchedTransactionsCommandHandler
    : IRequestHandler<ImportUnmatchedTransactionsCommand, ImportUnmatchedResult>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IBankCategoryMappingService _categoryMappingService;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IApplicationLogger<ImportUnmatchedTransactionsCommandHandler> _logger;

    /// <summary>
    /// Minimum confidence score for auto-categorization (transaction marked as reviewed).
    /// </summary>
    private const decimal AutoCategorizeMinConfidence = 0.9m;

    public ImportUnmatchedTransactionsCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        ITransactionRepository transactionRepository,
        IReconciliationAuditLogRepository auditLogRepository,
        IBankCategoryMappingService categoryMappingService,
        IBankConnectionRepository bankConnectionRepository,
        IApplicationLogger<ImportUnmatchedTransactionsCommandHandler> logger)
    {
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _transactionRepository = transactionRepository;
        _auditLogRepository = auditLogRepository;
        _categoryMappingService = categoryMappingService;
        _bankConnectionRepository = bankConnectionRepository;
        _logger = logger;
    }

    public async Task<ImportUnmatchedResult> Handle(
        ImportUnmatchedTransactionsCommand request,
        CancellationToken cancellationToken)
    {
        // Verify reconciliation exists and belongs to user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(
            request.ReconciliationId,
            request.UserId);

        if (reconciliation == null)
        {
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");
        }

        // Get reconciliation items
        var allItems = await _reconciliationItemRepository.GetByReconciliationIdAsync(
            request.ReconciliationId,
            request.UserId);

        // Filter to unmatched bank items only
        var unmatchedBankItems = allItems
            .Where(i => i.ItemType == ReconciliationItemType.UnmatchedBank)
            .ToList();

        // Further filter by specific IDs if provided
        IEnumerable<ReconciliationItem> itemsToImport;
        if (request.ImportAll)
        {
            itemsToImport = unmatchedBankItems;
        }
        else if (request.ItemIds?.Any() == true)
        {
            itemsToImport = unmatchedBankItems.Where(i => request.ItemIds.Contains(i.Id));
        }
        else
        {
            return new ImportUnmatchedResult
            {
                ImportedCount = 0,
                SkippedCount = 0,
                Errors = new[] { "No items specified for import" }
            };
        }

        var importedCount = 0;
        var skippedCount = 0;
        var createdTransactionIds = new List<int>();
        var errors = new List<string>();

        // Get provider ID for category mapping (default to "akahu" for Akahu reconciliations)
        string providerId = "akahu";
        var bankConnection = await _bankConnectionRepository.GetByAccountIdAsync(reconciliation.AccountId, cancellationToken);
        if (bankConnection != null)
        {
            providerId = bankConnection.ProviderId;
        }

        // Collect all bank categories from items to import
        var itemsWithData = itemsToImport.Select(item => new
        {
            Item = item,
            BankData = ParseBankReferenceData(item.BankReferenceData)
        }).ToList();

        var bankCategories = itemsWithData
            .Where(x => x.BankData != null && !string.IsNullOrEmpty(x.BankData.Category))
            .Select(x => x.BankData!.Category!)
            .Distinct()
            .ToList();

        // Resolve bank category mappings
        Dictionary<string, BankCategoryMappingResult> categoryMappings = new();
        if (bankCategories.Any())
        {
            _logger.LogInformation("Resolving bank category mappings for {Count} unique categories: [{Categories}]",
                bankCategories.Count, string.Join(", ", bankCategories.Select(c => $"'{c}'")));
            try
            {
                categoryMappings = await _categoryMappingService.ResolveAndCreateMappingsAsync(
                    bankCategories,
                    providerId,
                    request.UserId,
                    cancellationToken);

                var mappedCount = categoryMappings.Count(m => m.Value.CategoryId > 0);
                _logger.LogInformation("Resolved {MappedCount}/{TotalCount} bank categories to MyMascada categories",
                    mappedCount, bankCategories.Count);

                // Log each mapping for debugging
                foreach (var (cat, mapping) in categoryMappings)
                {
                    _logger.LogDebug("Category mapping: '{BankCategory}' -> CategoryId={CategoryId}, CategoryName='{CategoryName}', Confidence={Confidence}",
                        cat, mapping.CategoryId, mapping.CategoryName, mapping.ConfidenceScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve bank category mappings, transactions will flow through normal categorization");
            }
        }

        foreach (var item in itemsToImport)
        {
            try
            {
                // Skip if already has a linked transaction
                if (item.TransactionId.HasValue)
                {
                    _logger.LogDebug(
                        "Skipping item {ItemId} - already linked to transaction {TransactionId}",
                        item.Id, item.TransactionId);
                    skippedCount++;
                    continue;
                }

                // Parse the bank reference data
                var bankData = ParseBankReferenceData(item.BankReferenceData);
                if (bankData == null)
                {
                    _logger.LogWarning(
                        "Could not parse bank reference data for item {ItemId}",
                        item.Id);
                    errors.Add($"Could not parse data for item {item.Id}");
                    skippedCount++;
                    continue;
                }

                // Check for existing transaction with same external ID
                if (!string.IsNullOrEmpty(bankData.ExternalId))
                {
                    var existingTransaction = await _transactionRepository
                        .GetByExternalIdAsync(bankData.ExternalId);

                    if (existingTransaction != null)
                    {
                        _logger.LogDebug(
                            "Skipping item {ItemId} - transaction with external ID {ExternalId} already exists",
                            item.Id, bankData.ExternalId);

                        // Link the item to the existing transaction
                        item.TransactionId = existingTransaction.Id;
                        item.ItemType = ReconciliationItemType.Matched;
                        item.MatchMethod = MatchMethod.Manual;
                        item.MatchConfidence = 1.0m;
                        await _reconciliationItemRepository.UpdateAsync(item);

                        skippedCount++;
                        continue;
                    }
                }

                // Resolve category from bank category mapping
                int? resolvedCategoryId = null;
                bool isAutoReviewed = false;

                _logger.LogDebug("Processing item {ItemId}: BankCategory='{BankCategory}', HasCategory={HasCategory}, MappingsCount={MappingsCount}, MappingKeys=[{MappingKeys}]",
                    item.Id, bankData.Category ?? "(null)", !string.IsNullOrEmpty(bankData.Category), categoryMappings.Count,
                    string.Join(", ", categoryMappings.Keys.Select(k => $"'{k}'")));

                if (!string.IsNullOrEmpty(bankData.Category))
                {
                    var foundInMappings = categoryMappings.TryGetValue(bankData.Category, out var mapping);
                    _logger.LogInformation("Category lookup for item {ItemId}: BankCategory='{BankCategory}', Found={Found}, CategoryId={CategoryId}",
                        item.Id, bankData.Category, foundInMappings, mapping?.CategoryId ?? 0);

                    if (foundInMappings && mapping != null && mapping.CategoryId > 0)
                    {
                        resolvedCategoryId = mapping.CategoryId;
                        isAutoReviewed = mapping.ConfidenceScore >= AutoCategorizeMinConfidence;

                        _logger.LogInformation(
                            "Applied bank category mapping for item {ItemId}: '{BankCategory}' -> CategoryId {CategoryId} (confidence: {Confidence}, auto-reviewed: {AutoReviewed})",
                            item.Id, bankData.Category, mapping.CategoryId, mapping.ConfidenceScore, isAutoReviewed);
                    }
                    else
                    {
                        _logger.LogDebug("No valid mapping found for item {ItemId} with category '{BankCategory}'",
                            item.Id, bankData.Category);
                    }
                }

                // Create the new transaction
                var transaction = new Transaction
                {
                    Amount = NormalizeAmount(bankData.Amount),
                    TransactionDate = DateTimeProvider.ToUtc(bankData.Date),
                    Description = bankData.MerchantName ?? bankData.Description ?? "Unknown",
                    UserDescription = null,
                    Status = TransactionStatus.Cleared,
                    Source = TransactionSource.BankApi,
                    ExternalId = bankData.ExternalId,
                    ReferenceNumber = bankData.Reference,
                    Notes = BuildNotes(bankData),
                    AccountId = reconciliation.AccountId,
                    CategoryId = resolvedCategoryId,
                    IsReviewed = isAutoReviewed,
                    IsExcluded = false,
                    CreatedAt = DateTimeProvider.UtcNow,
                    UpdatedAt = DateTimeProvider.UtcNow
                };

                var createdTransaction = await _transactionRepository.AddAsync(transaction);
                createdTransactionIds.Add(createdTransaction.Id);

                // Link the reconciliation item to the new transaction
                item.TransactionId = createdTransaction.Id;
                item.ItemType = ReconciliationItemType.Matched;
                item.MatchMethod = MatchMethod.Manual;
                item.MatchConfidence = 1.0m;
                await _reconciliationItemRepository.UpdateAsync(item);

                importedCount++;
                _logger.LogDebug(
                    "Created transaction {TransactionId} from reconciliation item {ItemId}",
                    createdTransaction.Id, item.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import reconciliation item {ItemId}", item.Id);
                errors.Add($"Failed to import item {item.Id}: {ex.Message}");
                skippedCount++;
            }
        }

        // Create audit log entry
        var auditLog = new ReconciliationAuditLog
        {
            ReconciliationId = request.ReconciliationId,
            Action = ReconciliationAction.ManualTransactionAdded, // Use closest existing action type
            UserId = request.UserId,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        auditLog.SetDetails(new
        {
            Action = "ImportUnmatchedTransactions",
            ImportedCount = importedCount,
            SkippedCount = skippedCount,
            CreatedTransactionIds = createdTransactionIds,
            Errors = errors
        });

        await _auditLogRepository.AddAsync(auditLog);

        _logger.LogInformation(
            "Imported {ImportedCount} transactions from reconciliation {ReconciliationId}, skipped {SkippedCount}",
            importedCount, request.ReconciliationId, skippedCount);

        return new ImportUnmatchedResult
        {
            ImportedCount = importedCount,
            SkippedCount = skippedCount,
            CreatedTransactionIds = createdTransactionIds,
            Errors = errors
        };
    }

    private static BankTransactionData? ParseBankReferenceData(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Try to parse as our expected format first
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
            if (data == null)
                return null;

            return new BankTransactionData
            {
                ExternalId = GetStringValue(data, "externalId") ?? GetStringValue(data, "BankTransactionId"),
                Date = GetDateTimeValue(data, "date") ?? GetDateTimeValue(data, "TransactionDate") ?? DateTime.UtcNow,
                Amount = GetDecimalValue(data, "amount") ?? GetDecimalValue(data, "Amount") ?? 0,
                Description = GetStringValue(data, "description") ?? GetStringValue(data, "Description"),
                Reference = GetStringValue(data, "reference") ?? GetStringValue(data, "Reference"),
                Category = GetStringValue(data, "category") ?? GetStringValue(data, "BankCategory"),
                MerchantName = GetStringValue(data, "merchantName") ?? GetStringValue(data, "MerchantName")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetStringValue(Dictionary<string, JsonElement> data, string key)
    {
        if (data.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }
        return null;
    }

    private static DateTime? GetDateTimeValue(Dictionary<string, JsonElement> data, string key)
    {
        if (data.TryGetValue(key, out var element))
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(element.GetString(), out var result))
                    return result;
            }
        }
        return null;
    }

    private static decimal? GetDecimalValue(Dictionary<string, JsonElement> data, string key)
    {
        if (data.TryGetValue(key, out var element))
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetDecimal();
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                if (decimal.TryParse(element.GetString(), out var result))
                    return result;
            }
        }
        return null;
    }

    /// <summary>
    /// Normalizes the amount to ensure consistent sign convention:
    /// - Expenses are negative
    /// - Income is positive
    /// Akahu already uses this convention, so we just preserve the sign.
    /// </summary>
    private static decimal NormalizeAmount(decimal amount)
    {
        // Akahu uses standard convention: negative = expense, positive = income
        // We just return as-is since our database follows the same convention
        return amount;
    }

    private static string? BuildNotes(BankTransactionData data)
    {
        var notes = new List<string>();

        if (!string.IsNullOrEmpty(data.Category))
        {
            notes.Add($"Bank category: {data.Category}");
        }

        if (!string.IsNullOrEmpty(data.Reference))
        {
            notes.Add($"Reference: {data.Reference}");
        }

        notes.Add("Imported from Akahu reconciliation");

        return notes.Any() ? string.Join(" | ", notes) : null;
    }

    private class BankTransactionData
    {
        public string? ExternalId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public string? Category { get; set; }
        public string? MerchantName { get; set; }
    }
}
