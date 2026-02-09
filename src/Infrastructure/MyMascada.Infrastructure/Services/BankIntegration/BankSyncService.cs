using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Application.Features.ImportReview.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.BankIntegration;

/// <summary>
/// Service for orchestrating bank synchronization operations.
/// Coordinates between bank providers and the import analysis pipeline.
/// </summary>
public class BankSyncService : IBankSyncService
{
    private readonly IBankProviderFactory _providerFactory;
    private readonly IBankConnectionRepository _connectionRepository;
    private readonly IBankSyncLogRepository _syncLogRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IImportAnalysisService _importAnalysisService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBankCategoryMappingService _categoryMappingService;
    private readonly IApplicationLogger<BankSyncService> _logger;

    /// <summary>
    /// Default sync window: last 30 days
    /// </summary>
    private const int DefaultSyncDays = 30;

    /// <summary>
    /// Days to overlap with last sync to catch any transactions that may have been delayed
    /// </summary>
    private const int SyncOverlapDays = 3;

    /// <summary>
    /// Minimum confidence score for auto-categorization (transaction marked as reviewed).
    /// </summary>
    private const decimal AutoCategorizeMinConfidence = 0.9m;

    public BankSyncService(
        IBankProviderFactory providerFactory,
        IBankConnectionRepository connectionRepository,
        IBankSyncLogRepository syncLogRepository,
        ISettingsEncryptionService encryptionService,
        IImportAnalysisService importAnalysisService,
        ITransactionRepository transactionRepository,
        IBankCategoryMappingService categoryMappingService,
        IApplicationLogger<BankSyncService> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _syncLogRepository = syncLogRepository ?? throw new ArgumentNullException(nameof(syncLogRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _importAnalysisService = importAnalysisService ?? throw new ArgumentNullException(nameof(importAnalysisService));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _categoryMappingService = categoryMappingService ?? throw new ArgumentNullException(nameof(categoryMappingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<BankSyncResult> SyncAccountAsync(int bankConnectionId, BankSyncType syncType, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;

        // 1. Get the bank connection
        var connection = await _connectionRepository.GetByIdAsync(bankConnectionId, ct);
        if (connection == null)
        {
            _logger.LogWarning("Bank connection {ConnectionId} not found", bankConnectionId);
            return BankSyncResult.Failure(bankConnectionId, 0, "Bank connection not found", startedAt);
        }

        // 2. Create sync log entry (InProgress)
        var syncLog = new BankSyncLog
        {
            BankConnectionId = bankConnectionId,
            SyncType = syncType,
            Status = BankSyncStatus.InProgress,
            StartedAt = startedAt,
            TransactionsProcessed = 0,
            TransactionsImported = 0,
            TransactionsSkipped = 0
        };
        syncLog = await _syncLogRepository.AddAsync(syncLog, ct);

        try
        {
            // 3. Get the provider
            var provider = _providerFactory.GetProvider(connection.ProviderId);
            if (provider == null)
            {
                return await CompleteSyncWithErrorAsync(syncLog, connection, $"Bank provider '{connection.ProviderId}' not found", ct);
            }

            // 4. Build config for provider
            var config = BuildConnectionConfig(connection);

            // 5. Calculate date range (last 30 days by default, or from last sync with overlap)
            var to = DateTime.UtcNow.Date.AddDays(1); // Include today's transactions
            var from = connection.LastSyncAt?.Date.AddDays(-SyncOverlapDays) ?? to.AddDays(-DefaultSyncDays);

            _logger.LogInformation(
                "Starting bank sync for connection {ConnectionId} ({ProviderId}) from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}",
                bankConnectionId, connection.ProviderId, from, to);

            // 6. Fetch transactions from provider
            var fetchResult = await provider.FetchTransactionsAsync(config, from, to, ct);
            if (!fetchResult.IsSuccess)
            {
                _logger.LogWarning("Failed to fetch transactions for connection {ConnectionId}: {Error}",
                    bankConnectionId, fetchResult.ErrorMessage ?? "Unknown error");
                return await CompleteSyncWithErrorAsync(syncLog, connection, fetchResult.ErrorMessage ?? "Failed to fetch transactions", ct);
            }

            syncLog.TransactionsProcessed = fetchResult.Transactions.Count;
            _logger.LogInformation("Fetched {Count} transactions from {ProviderId}",
                fetchResult.Transactions.Count, connection.ProviderId);

            // 7. Handle case where no transactions were fetched
            if (fetchResult.Transactions.Count == 0)
            {
                _logger.LogInformation("No transactions to process for connection {ConnectionId}", bankConnectionId);
                return await CompleteSyncSuccessAsync(syncLog, connection, 0, 0, ct);
            }

            // 8. Map to ImportCandidateDto for duplicate detection
            var candidates = fetchResult.Transactions
                .Select(tx => MapToImportCandidate(tx))
                .ToList();

            // 9. Use ImportAnalysisService for duplicate detection
            var analysisRequest = new AnalyzeImportRequest
            {
                Source = "bankapi",
                AccountId = connection.AccountId,
                UserId = connection.UserId,
                Candidates = candidates,
                Options = new ImportAnalysisOptions
                {
                    DateToleranceDays = 0, // Exact date match for bank API (bank APIs provide precise dates)
                    AmountTolerance = 0m, // Exact amount match
                    DescriptionSimilarityThreshold = 0.5,
                    IncludeManualTransactions = true,
                    IncludeRecentImports = true
                }
            };

            var analysisResult = await _importAnalysisService.AnalyzeImportAsync(analysisRequest);

            // 10. Resolve bank category mappings for transactions that have categories
            var bankCategories = fetchResult.Transactions
                .Where(tx => !string.IsNullOrEmpty(tx.Category))
                .Select(tx => tx.Category!)
                .Distinct()
                .ToList();

            Dictionary<string, BankCategoryMappingResult> categoryMappings = new();
            if (bankCategories.Any())
            {
                _logger.LogInformation("Resolving bank category mappings for {Count} unique categories", bankCategories.Count);
                try
                {
                    categoryMappings = await _categoryMappingService.ResolveAndCreateMappingsAsync(
                        bankCategories,
                        connection.ProviderId,
                        connection.UserId,
                        ct);

                    var mappedCount = categoryMappings.Count(m => m.Value.CategoryId > 0);
                    _logger.LogInformation("Resolved {MappedCount}/{TotalCount} bank categories to MyMascada categories",
                        mappedCount, bankCategories.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve bank category mappings, transactions will flow through normal categorization");
                }
            }

            // 11. Auto-import clean transactions, skip duplicates
            var imported = 0;
            var skipped = 0;
            var importedTransactionIds = new List<int>();

            foreach (var item in analysisResult.ReviewItems)
            {
                // Check for exact duplicates (same external reference ID) or other high-confidence duplicates
                var hasExactDuplicate = item.Conflicts.Any(c =>
                    c.Type == ConflictType.ExactDuplicate ||
                    (c.Type == ConflictType.PotentialDuplicate && c.ConfidenceScore >= 0.95m));

                if (hasExactDuplicate)
                {
                    _logger.LogDebug(
                        "Skipping duplicate transaction: {Description} ({Amount:C}) - ExternalId: {ExternalId}",
                        item.ImportCandidate.Description,
                        item.ImportCandidate.Amount,
                        item.ImportCandidate.ExternalReferenceId ?? "(none)");
                    skipped++;
                    continue;
                }

                // Create the transaction
                try
                {
                    var transaction = await CreateTransactionFromCandidateAsync(
                        item.ImportCandidate,
                        connection.AccountId,
                        connection.UserId,
                        categoryMappings,
                        ct);

                    importedTransactionIds.Add(transaction.Id);

                    _logger.LogDebug(
                        "Imported transaction {TransactionId}: {Description} ({Amount:C})",
                        transaction.Id,
                        transaction.Description,
                        transaction.Amount);

                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to create transaction from candidate: {Description} ({Amount:C})",
                        item.ImportCandidate.Description,
                        item.ImportCandidate.Amount);
                    // Continue processing other transactions
                }
            }

            _logger.LogInformation(
                "Bank sync completed for connection {ConnectionId}: {Imported} imported, {Skipped} skipped (duplicates)",
                bankConnectionId, imported, skipped);

            return await CompleteSyncSuccessAsync(syncLog, connection, imported, skipped, ct, importedTransactionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bank sync failed for connection {ConnectionId}", bankConnectionId);
            return await CompleteSyncWithErrorAsync(syncLog, connection, $"Sync failed: {ex.Message}", ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BankSyncResult>> SyncAllConnectionsAsync(Guid userId, BankSyncType syncType, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting sync of all active connections for user {UserId}", userId);

        var connections = await _connectionRepository.GetActiveByUserIdAsync(userId, ct);
        var connectionsList = connections.ToList();

        if (!connectionsList.Any())
        {
            _logger.LogInformation("No active bank connections found for user {UserId}", userId);
            return Enumerable.Empty<BankSyncResult>();
        }

        _logger.LogInformation("Found {Count} active bank connections to sync for user {UserId}",
            connectionsList.Count, userId);

        var results = new List<BankSyncResult>();

        foreach (var connection in connectionsList)
        {
            try
            {
                var result = await SyncAccountAsync(connection.Id, syncType, ct);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing connection {ConnectionId} for user {UserId}",
                    connection.Id, userId);

                // Create a failure result for this connection
                results.Add(BankSyncResult.Failure(
                    connection.Id,
                    0,
                    $"Unexpected error: {ex.Message}",
                    DateTime.UtcNow));
            }
        }

        var successful = results.Count(r => r.IsSuccess);
        var failed = results.Count - successful;
        var totalImported = results.Sum(r => r.TransactionsImported);
        var totalSkipped = results.Sum(r => r.TransactionsSkipped);

        _logger.LogInformation(
            "Completed sync of all connections for user {UserId}: {Successful} successful, {Failed} failed, {Imported} transactions imported, {Skipped} skipped",
            userId, successful, failed, totalImported, totalSkipped);

        return results;
    }

    /// <summary>
    /// Builds the connection config for the provider from the bank connection entity.
    /// </summary>
    private BankConnectionConfig BuildConnectionConfig(BankConnection connection)
    {
        var settings = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(connection.EncryptedSettings))
        {
            // Pass the encrypted settings to the provider
            // The provider will use the encryption service to decrypt if needed
            settings["encrypted"] = connection.EncryptedSettings;
        }

        return new BankConnectionConfig
        {
            BankConnectionId = connection.Id,
            AccountId = connection.AccountId,
            UserId = connection.UserId,
            ProviderId = connection.ProviderId,
            ExternalAccountId = connection.ExternalAccountId,
            Settings = settings
        };
    }

    /// <summary>
    /// Maps a bank transaction DTO to an import candidate DTO for duplicate detection.
    /// </summary>
    private static ImportCandidateDto MapToImportCandidate(BankTransactionDto tx)
    {
        // Akahu and most bank APIs use standard convention: negative = expense, positive = income
        var isIncome = tx.Amount > 0;

        return new ImportCandidateDto
        {
            TempId = Guid.NewGuid().ToString(),
            Amount = tx.Amount, // Already correctly signed from bank API
            Date = tx.Date,
            Description = tx.Description ?? "Bank transaction",
            ExternalReferenceId = tx.ExternalId, // Critical for duplicate detection
            ReferenceId = tx.Reference,
            Type = isIncome ? TransactionType.Income : TransactionType.Expense,
            Status = TransactionStatus.Cleared,
            Category = tx.Category,
            Notes = BuildNotes(tx)
        };
    }

    /// <summary>
    /// Builds notes from transaction metadata.
    /// </summary>
    private static string? BuildNotes(BankTransactionDto tx)
    {
        if (tx.Metadata == null || !tx.Metadata.Any())
            return null;

        var notes = new List<string>();

        if (tx.Metadata.TryGetValue("otherAccount", out var otherAccount) && otherAccount != null)
            notes.Add($"Other party: {otherAccount}");

        if (tx.Metadata.TryGetValue("foreignAmount", out var foreignAmount) &&
            tx.Metadata.TryGetValue("foreignCurrency", out var foreignCurrency) &&
            foreignAmount != null && foreignCurrency != null)
            notes.Add($"Foreign: {foreignAmount} {foreignCurrency}");

        if (tx.Metadata.TryGetValue("akahuCategoryGroup", out var categoryGroup) && categoryGroup != null)
            notes.Add($"Akahu category: {tx.Category} ({categoryGroup})");

        if (tx.MerchantName != null)
            notes.Add($"Merchant: {tx.MerchantName}");

        return notes.Count != 0 ? string.Join("\n", notes) : null;
    }

    /// <summary>
    /// Creates a transaction entity from an import candidate.
    /// Applies bank category mapping if available.
    /// </summary>
    private async Task<Transaction> CreateTransactionFromCandidateAsync(
        ImportCandidateDto candidate,
        int accountId,
        Guid userId,
        Dictionary<string, BankCategoryMappingResult> categoryMappings,
        CancellationToken ct)
    {
        // Determine category and review status from bank category mapping
        int? categoryId = null;
        bool isReviewed = false;

        if (!string.IsNullOrEmpty(candidate.Category) &&
            categoryMappings.TryGetValue(candidate.Category, out var mappingResult) &&
            mappingResult.CategoryId > 0)
        {
            categoryId = mappingResult.CategoryId;

            // Auto-review if confidence is high enough
            if (mappingResult.ConfidenceScore >= AutoCategorizeMinConfidence)
            {
                isReviewed = true;
                _logger.LogDebug(
                    "Auto-categorizing transaction '{Description}' to category {CategoryId} ({CategoryName}) with confidence {Confidence:P0}",
                    candidate.Description, categoryId, mappingResult.CategoryName, mappingResult.ConfidenceScore);
            }
            else
            {
                _logger.LogDebug(
                    "Pre-filling category {CategoryId} ({CategoryName}) for transaction '{Description}' - requires review (confidence: {Confidence:P0})",
                    categoryId, mappingResult.CategoryName, candidate.Description, mappingResult.ConfidenceScore);
            }

            // Record the mapping application for stats tracking
            if (mappingResult.Mapping != null)
            {
                try
                {
                    await _categoryMappingService.RecordMappingApplicationAsync(mappingResult.Mapping.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to record mapping application for mapping {MappingId}", mappingResult.Mapping.Id);
                }
            }
        }

        // Note: UserId is not stored on Transaction entity - it's derived from the Account relationship
        var transaction = new Transaction
        {
            AccountId = accountId,
            TransactionDate = candidate.Date,
            Description = candidate.Description ?? "Bank transaction",
            Amount = candidate.Amount, // Already normalized by ImportAnalysisService
            Type = candidate.Type,
            Status = candidate.Status,
            Source = TransactionSource.BankApi, // Critical: Mark as BankApi source
            ExternalId = candidate.ExternalReferenceId,
            ReferenceNumber = candidate.ReferenceId,
            Notes = candidate.Notes,
            CategoryId = categoryId,
            IsReviewed = isReviewed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedTransaction = await _transactionRepository.AddAsync(transaction);
        await _transactionRepository.SaveChangesAsync();

        return savedTransaction;
    }

    /// <summary>
    /// Completes the sync operation successfully and updates the sync log and connection.
    /// </summary>
    private async Task<BankSyncResult> CompleteSyncSuccessAsync(
        BankSyncLog syncLog,
        BankConnection connection,
        int imported,
        int skipped,
        CancellationToken ct,
        List<int>? importedTransactionIds = null)
    {
        syncLog.Status = BankSyncStatus.Completed;
        syncLog.CompletedAt = DateTime.UtcNow;
        syncLog.TransactionsImported = imported;
        syncLog.TransactionsSkipped = skipped;
        await _syncLogRepository.UpdateAsync(syncLog, ct);

        connection.LastSyncAt = DateTime.UtcNow;
        connection.LastSyncError = null;
        await _connectionRepository.UpdateAsync(connection, ct);

        return BankSyncResult.Success(
            connection.Id,
            syncLog.Id,
            syncLog.TransactionsProcessed,
            imported,
            skipped,
            syncLog.StartedAt,
            importedTransactionIds);
    }

    /// <summary>
    /// Completes the sync operation with an error and updates the sync log and connection.
    /// </summary>
    private async Task<BankSyncResult> CompleteSyncWithErrorAsync(
        BankSyncLog syncLog,
        BankConnection connection,
        string error,
        CancellationToken ct)
    {
        syncLog.Status = BankSyncStatus.Failed;
        syncLog.CompletedAt = DateTime.UtcNow;
        syncLog.ErrorMessage = error;
        await _syncLogRepository.UpdateAsync(syncLog, ct);

        connection.LastSyncError = error;
        await _connectionRepository.UpdateAsync(connection, ct);

        return BankSyncResult.Failure(connection.Id, syncLog.Id, error, syncLog.StartedAt);
    }
}
