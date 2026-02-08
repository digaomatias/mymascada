using System.Text.Json;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record BulkApproveMatchesCommand : IRequest<BulkApproveMatchesResult>
{
    public Guid UserId { get; init; }
    public int ReconciliationId { get; init; }
    public decimal MinConfidenceThreshold { get; init; } = 0.95m;
    public IEnumerable<int>? SpecificItemIds { get; init; }
}

public class BulkApproveMatchesResult
{
    public int ApprovedCount { get; set; }
    public int EnrichedCount { get; set; }
    public int CategorizedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class BulkApproveMatchesCommandHandler : IRequestHandler<BulkApproveMatchesCommand, BulkApproveMatchesResult>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IApplicationLogger<BulkApproveMatchesCommandHandler> _logger;

    public BulkApproveMatchesCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService,
        IApplicationLogger<BulkApproveMatchesCommandHandler> logger)
    {
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
        _logger = logger;
    }

    public async Task<BulkApproveMatchesResult> Handle(BulkApproveMatchesCommand request, CancellationToken cancellationToken)
    {
        var result = new BulkApproveMatchesResult();

        // Verify reconciliation exists and belongs to user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");

        // Verify the user has modify permission on the reconciliation's account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, reconciliation.AccountId))
            throw new UnauthorizedAccessException("You do not have permission to approve matches on this account.");

        _logger.LogInformation(
            "Starting bulk approval for reconciliation {ReconciliationId}, min confidence {MinConfidence}",
            request.ReconciliationId, request.MinConfidenceThreshold);

        // Get all reconciliation items for this reconciliation
        var allItems = await _reconciliationItemRepository.GetByReconciliationIdAsync(request.ReconciliationId, request.UserId);
        var itemsList = allItems.ToList();

        // Filter items to approve based on criteria
        var itemsToApprove = itemsList.Where(item =>
        {
            // Only process matched items that aren't already approved
            if (item.ItemType != ReconciliationItemType.Matched || item.IsApproved)
                return false;

            // Must have a linked transaction
            if (!item.TransactionId.HasValue)
                return false;

            // If specific IDs provided, only process those
            if (request.SpecificItemIds?.Any() == true)
                return request.SpecificItemIds.Contains(item.Id);

            // Otherwise, use confidence threshold
            return item.MatchConfidence >= request.MinConfidenceThreshold;
        }).ToList();

        if (!itemsToApprove.Any())
        {
            _logger.LogInformation("No items found matching approval criteria");
            return result;
        }

        _logger.LogInformation("Found {Count} items to approve", itemsToApprove.Count);

        // Collect items with bank data for processing
        var itemsWithBankData = new List<(Domain.Entities.ReconciliationItem Item, BankTransactionData BankData)>();

        foreach (var item in itemsToApprove)
        {
            var bankData = ParseBankReferenceData(item.BankReferenceData);
            if (bankData != null)
            {
                itemsWithBankData.Add((item, bankData));
            }
        }

        // Process each item - enrich with bank data only
        // Categorization is handled by the CategorizationPipeline (Rules -> BankCategory -> ML -> LLM)
        foreach (var (item, bankData) in itemsWithBankData)
        {
            try
            {
                // Get the linked transaction
                var transaction = await _transactionRepository.GetByIdAsync(item.TransactionId!.Value, request.UserId);
                if (transaction == null)
                {
                    _logger.LogWarning("Transaction {TransactionId} not found for item {ItemId}", item.TransactionId, item.Id);
                    result.Errors.Add($"Transaction {item.TransactionId} not found");
                    result.SkippedCount++;
                    continue;
                }

                bool wasEnriched = false;

                // Enrich transaction with bank data
                if (!string.IsNullOrEmpty(bankData.ExternalId) && string.IsNullOrEmpty(transaction.ExternalId))
                {
                    transaction.ExternalId = bankData.ExternalId;
                    wasEnriched = true;
                    _logger.LogDebug("Set ExternalId {ExternalId} on transaction {TransactionId}", bankData.ExternalId, transaction.Id);
                }

                // Update reference number if not set
                if (!string.IsNullOrEmpty(bankData.Reference) && string.IsNullOrEmpty(transaction.ReferenceNumber))
                {
                    transaction.ReferenceNumber = bankData.Reference;
                    wasEnriched = true;
                }

                // Set bank category field if present (used by categorization pipeline)
                if (!string.IsNullOrEmpty(bankData.Category) && string.IsNullOrEmpty(transaction.BankCategory))
                {
                    transaction.BankCategory = bankData.Category;
                    wasEnriched = true;
                    _logger.LogDebug(
                        "Set BankCategory '{BankCategory}' on transaction {TransactionId} for later categorization by pipeline",
                        bankData.Category, transaction.Id);
                }

                // Mark transaction as reconciled
                transaction.Status = TransactionStatus.Reconciled;

                // Save transaction changes (categorization handled by CategorizationPipeline)
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);

                // Mark the reconciliation item as approved
                item.IsApproved = true;
                item.ApprovedAt = DateTime.UtcNow;
                await _reconciliationItemRepository.UpdateAsync(item);

                result.ApprovedCount++;
                if (wasEnriched) result.EnrichedCount++;

                _logger.LogDebug(
                    "Approved item {ItemId}: enriched={Enriched}, status=Reconciled",
                    item.Id, wasEnriched);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process approval for item {ItemId}", item.Id);
                result.Errors.Add($"Failed to process item {item.Id}: {ex.Message}");
                result.SkippedCount++;
            }
        }

        _logger.LogInformation(
            "Bulk approval completed: approved={Approved}, enriched={Enriched}, categorized={Categorized}, skipped={Skipped}",
            result.ApprovedCount, result.EnrichedCount, result.CategorizedCount, result.SkippedCount);

        return result;
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
