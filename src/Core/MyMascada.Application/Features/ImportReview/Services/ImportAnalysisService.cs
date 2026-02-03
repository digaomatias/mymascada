using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.ImportReview.DTOs;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.ImportReview.Services;

public class ImportAnalysisService : IImportAnalysisService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IApplicationLogger<ImportAnalysisService> _logger;
    private readonly Dictionary<string, ImportAnalysisResult> _analysisCache = new();

    public ImportAnalysisService(
        ITransactionRepository transactionRepository,
        IApplicationLogger<ImportAnalysisService> logger)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<ImportAnalysisResult> AnalyzeImportAsync(AnalyzeImportRequest request)
    {
        try
        {
            var analysisId = Guid.NewGuid().ToString();
            var candidatesList = request.Candidates.ToList();
            _logger.LogInformation("Starting import analysis for {CandidateCount} candidates", candidatesList.Count);

            // CRITICAL: Normalize candidate amounts BEFORE duplicate detection
            // This ensures we compare normalized amounts with existing transactions
            candidatesList = candidatesList.Select(candidate =>
            {
                var normalizedAmount = candidate.Type == TransactionType.Expense
                    ? -Math.Abs(candidate.Amount)  // Always negative for expenses
                    : Math.Abs(candidate.Amount);  // Always positive for income
                
                if (candidate.Amount != normalizedAmount)
                {
                    _logger.LogInformation("Normalized candidate amount: '{Description}' from {OriginalAmount:C} to {NormalizedAmount:C} (Type: {Type})", 
                        candidate.Description, candidate.Amount, normalizedAmount, candidate.Type);
                    
                    // Create new candidate with normalized amount
                    return candidate with { Amount = normalizedAmount };
                }
                
                return candidate;
            }).ToList();

            // Handle empty candidate list
            if (!candidatesList.Any())
            {
                _logger.LogWarning("No candidates provided for import analysis");
                return new ImportAnalysisResult
                {
                    AnalysisId = analysisId,
                    AccountId = request.AccountId,
                    ReviewItems = new List<ImportReviewItemDto>(),
                    Summary = new ImportAnalysisStatistics(),
                    AnalysisNotes = new[] { "No transactions found in the import data" },
                    Warnings = new[] { "Import file appears to be empty or no valid transactions were found" },
                    Errors = new List<string>(),
                    AnalyzedAt = DateTime.UtcNow
                };
            }

            // Get existing transactions for the account with broader date range
            var earliestDate = candidatesList.Min(c => c.Date).AddDays(-request.Options.DateToleranceDays);
            var latestDate = candidatesList.Max(c => c.Date).AddDays(request.Options.DateToleranceDays);
            
            var existingTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(
                request.AccountId, earliestDate, latestDate, request.UserId);
            
            _logger.LogInformation("Duplicate detection: Found {ExistingCount} existing transactions for account {AccountId} in date range {StartDate} to {EndDate}. UserId: {UserId}", 
                existingTransactions.Count(), request.AccountId, earliestDate.ToString("yyyy-MM-dd"), latestDate.ToString("yyyy-MM-dd"), request.UserId);
            
            // Debug log first few existing transactions
            if (existingTransactions.Any())
            {
                foreach (var existing in existingTransactions.Take(5))
                {
                    _logger.LogInformation("Existing transaction: ID={Id}, Description='{Description}', Amount={Amount:C}, Date={Date:yyyy-MM-dd}, AccountId={AccountId}", 
                        existing.Id, existing.Description, existing.Amount, existing.TransactionDate, existing.AccountId);
                }
            }
            else
            {
                _logger.LogWarning("NO existing transactions found for duplicate detection. This may indicate an issue.");
            }

            var reviewItems = new List<ImportReviewItemDto>();

            foreach (var candidate in candidatesList)
            {
                _logger.LogInformation("Processing candidate: Description='{Description}', Amount={Amount:C}, Date={Date:yyyy-MM-dd}, Type={Type}", 
                    candidate.Description, candidate.Amount, candidate.Date, candidate.Type);
                
                var conflicts = await DetectConflictsAsync(candidate, existingTransactions, request.Options);
                
                _logger.LogInformation("Candidate {Description} ({Amount:C} on {Date:yyyy-MM-dd}): {ConflictCount} conflicts detected", 
                    candidate.Description, candidate.Amount, candidate.Date, conflicts.Count());
                
                // Debug log conflicts if any found
                if (conflicts.Any())
                {
                    foreach (var conflict in conflicts)
                    {
                        _logger.LogInformation("Conflict found: Type={ConflictType}, Severity={Severity}, Message='{Message}', Confidence={Confidence}", 
                            conflict.Type, conflict.Severity, conflict.Message, conflict.ConfidenceScore);
                    }
                }
                
                var reviewItem = new ImportReviewItemDto
                {
                    Id = Guid.NewGuid().ToString(),
                    ImportCandidate = candidate,
                    Conflicts = conflicts,
                    ReviewDecision = conflicts.Any() ? ConflictResolution.Pending : ConflictResolution.Import,
                    IsProcessed = false
                };

                reviewItems.Add(reviewItem);
            }

            var statistics = CalculateStatistics(reviewItems);
            var analysisNotes = GenerateAnalysisNotes(reviewItems, request.Options);

            var (warnings, errors) = ExtractWarningsAndErrors(reviewItems, request.Options);

            var result = new ImportAnalysisResult
            {
                AnalysisId = analysisId,
                AccountId = request.AccountId, // Store account ID for later execution
                ReviewItems = reviewItems,
                Summary = statistics,
                AnalysisNotes = analysisNotes,
                Warnings = warnings,
                Errors = errors,
                AnalyzedAt = DateTime.UtcNow
            };

            // Cache the result for later execution
            _analysisCache[analysisId] = result;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing import for account {AccountId}", request.AccountId);
            throw;
        }
    }

    public async Task<ImportExecutionResult> ExecuteImportAsync(ImportExecutionRequest request)
    {
        try
        {
            // Try to get the cached analysis result first
            _analysisCache.TryGetValue(request.AnalysisId, out var analysisResult);
            
            // Get account ID - prioritize request AccountId over cached analysis
            var accountId = request.AccountId > 0 ? request.AccountId : analysisResult?.AccountId ?? 0;
            if (accountId == 0)
            {
                throw new InvalidOperationException("Invalid account ID - please provide AccountId in request or ensure analysis cache is available");
            }
            
            _logger.LogInformation("Executing import for account {AccountId} with analysis {AnalysisId} (cached: {HasCache})", 
                accountId, request.AnalysisId, analysisResult != null);

            // Log cache status but continue processing
            if (analysisResult == null)
            {
                _logger.LogWarning("Analysis {AnalysisId} not found in cache - likely expired. Will attempt to process using candidate data from decisions.", request.AnalysisId);
            }

            var importedTransactions = new List<ImportedTransactionDto>();
            var errors = new List<string>();
            var warnings = new List<string>();
            
            int importedCount = 0;
            int skippedCount = 0;
            int mergedCount = 0;
            int errorCount = 0;

            // Create a lookup for review items by ID (if analysis cache is available)
            var reviewItemLookup = analysisResult?.ReviewItems?.ToDictionary(r => r.Id, r => r) ?? new Dictionary<string, ImportReviewItemDto>();

            foreach (var decision in request.Decisions)
            {
                try
                {
                    // Try to get from cache first, but fall back to decision candidate data
                    ImportCandidateDto candidate = null;
                    if (reviewItemLookup.TryGetValue(decision.ReviewItemId, out var reviewItem))
                    {
                        candidate = reviewItem.ImportCandidate;
                    }
                    else if (decision.Candidate != null)
                    {
                        candidate = decision.Candidate;
                        _logger.LogDebug("Using candidate data from decision for review item {ReviewItemId} (cache miss)", decision.ReviewItemId);
                    }
                    else
                    {
                        errors.Add($"Review item {decision.ReviewItemId} not found in analysis and no candidate data provided");
                        errorCount++;
                        continue;
                    }

                    switch (decision.Decision)
                    {
                        case ConflictResolution.Import:
                            // Create new transaction from candidate
                            var newTransaction = await CreateTransactionFromCandidateAsync(
                                candidate, accountId);
                            
                            var importedDto = MapToImportedTransactionDto(newTransaction);
                            importedTransactions.Add(importedDto);
                            importedCount++;
                            
                            _logger.LogInformation("Imported transaction {TransactionId} for {Amount}", 
                                newTransaction.Id, newTransaction.Amount);
                            break;

                        case ConflictResolution.Skip:
                            skippedCount++;
                            _logger.LogDebug("Skipped transaction for review item {ReviewItemId}", decision.ReviewItemId);
                            break;

                        case ConflictResolution.MergeWithExisting:
                            // Find the existing transaction to merge with
                            ExistingTransactionDto mergeTarget = null;
                            if (reviewItem != null)
                            {
                                mergeTarget = reviewItem.Conflicts
                                    .Where(c => c.ConflictingTransaction != null)
                                    .Select(c => c.ConflictingTransaction)
                                    .FirstOrDefault();
                            }
                            else
                            {
                                // When we don't have cached review item, we need to re-detect conflicts to find merge target
                                // This is a fallback - in practice, merge decisions should come with conflict data
                                var existingTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(
                                    accountId, candidate.Date.AddDays(-3), candidate.Date.AddDays(3), request.UserId);
                                
                                var conflicts = await DetectConflictsAsync(candidate, existingTransactions, new ImportAnalysisOptions());
                                mergeTarget = conflicts
                                    .Where(c => c.ConflictingTransaction != null)
                                    .Select(c => c.ConflictingTransaction)
                                    .FirstOrDefault();
                            }
                            
                            if (mergeTarget != null)
                            {
                                await MergeTransactionAsync(candidate, mergeTarget, request.UserId);
                                mergedCount++;
                                warnings.Add($"Merged transaction data for item {decision.ReviewItemId}");
                            }
                            else
                            {
                                errors.Add($"No existing transaction found to merge with for item {decision.ReviewItemId}");
                                errorCount++;
                            }
                            break;

                        case ConflictResolution.ReplaceExisting:
                            // Find the existing transaction to replace
                            ExistingTransactionDto replaceTarget = null;
                            if (reviewItem != null)
                            {
                                replaceTarget = reviewItem.Conflicts
                                    .Where(c => c.ConflictingTransaction != null)
                                    .Select(c => c.ConflictingTransaction)
                                    .FirstOrDefault();
                            }
                            else
                            {
                                // When we don't have cached review item, we need to re-detect conflicts to find replace target
                                var existingTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(
                                    accountId, candidate.Date.AddDays(-3), candidate.Date.AddDays(3), request.UserId);
                                
                                var conflicts = await DetectConflictsAsync(candidate, existingTransactions, new ImportAnalysisOptions());
                                replaceTarget = conflicts
                                    .Where(c => c.ConflictingTransaction != null)
                                    .Select(c => c.ConflictingTransaction)
                                    .FirstOrDefault();
                            }
                            
                            if (replaceTarget != null)
                            {
                                await ReplaceTransactionAsync(candidate, replaceTarget, accountId, request.UserId);
                                importedCount++;
                                warnings.Add($"Replaced existing transaction for item {decision.ReviewItemId}");
                            }
                            else
                            {
                                errors.Add($"No existing transaction found to replace for item {decision.ReviewItemId}");
                                errorCount++;
                            }
                            break;

                        default:
                            errors.Add($"Invalid decision {decision.Decision} for item {decision.ReviewItemId}");
                            errorCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing decision for item {ReviewItemId}", decision.ReviewItemId);
                    errors.Add($"Error processing item {decision.ReviewItemId}: {ex.Message}");
                    errorCount++;
                }
            }

            // Clear the analysis from cache after execution
            _analysisCache.Remove(request.AnalysisId);

            var statistics = new ImportExecutionStatistics
            {
                TotalDecisions = request.Decisions.Count(),
                ImportedCount = importedCount,
                SkippedCount = skippedCount,
                MergedCount = mergedCount,
                ErrorCount = errorCount,
                ExecutedAt = DateTime.UtcNow
            };

            var result = new ImportExecutionResult
            {
                IsSuccess = errorCount == 0,
                Message = errorCount == 0 ? "Import completed successfully" : $"Import completed with {errorCount} errors",
                Statistics = statistics,
                Errors = errors,
                Warnings = warnings,
                ImportedTransactions = importedTransactions
            };

            _logger.LogInformation("Import execution completed: {ImportedCount} imported, {SkippedCount} skipped, {MergedCount} merged, {ErrorCount} errors", 
                importedCount, skippedCount, mergedCount, errorCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing import {AnalysisId}", request.AnalysisId);
            throw;
        }
    }

    public async Task<BulkActionResult> ApplyBulkActionAsync(BulkActionRequest request)
    {
        // TODO: In a real implementation, we would:
        // 1. Retrieve the analysis from database
        // 2. Update the review decisions in the database
        // 3. Return the count of affected items
        
        await Task.CompletedTask; // Remove warning about async without await

        return new BulkActionResult
        {
            IsSuccess = true,
            AffectedItemsCount = 0, // Would be calculated from actual database operation
            Message = $"Bulk action {request.ActionType} applied successfully"
        };
    }

    private async Task<List<ConflictInfoDto>> DetectConflictsAsync(
        ImportCandidateDto candidate, 
        IEnumerable<Transaction> existingTransactions, 
        ImportAnalysisOptions options)
    {
        var conflicts = new List<ConflictInfoDto>();

        foreach (var existing in existingTransactions)
        {
            // Check for exact duplicates (same external reference ID)
            if (!string.IsNullOrEmpty(candidate.ExternalReferenceId) && 
                candidate.ExternalReferenceId == existing.ExternalId)
            {
                conflicts.Add(new ConflictInfoDto
                {
                    Type = ConflictType.ExactDuplicate,
                    Severity = ConflictSeverity.High,
                    Message = "Transaction with same external reference ID already exists",
                    ConflictingTransaction = MapToExistingTransactionDto(existing),
                    ConfidenceScore = 1.0m
                });
                continue;
            }

            // Check for potential duplicates (amount + date proximity)
            var daysDifference = Math.Abs((candidate.Date - existing.TransactionDate).TotalDays);
            var amountDifference = Math.Abs(candidate.Amount - existing.Amount);

            if (daysDifference <= options.DateToleranceDays && amountDifference <= options.AmountTolerance)
            {
                var similarity = CalculateDescriptionSimilarity(candidate.Description, existing.Description);
                
                // Key change: Always flag as potential duplicate if amount and date match closely
                // Description is secondary - important for identifying duplicates with empty/edited descriptions
                var isExactAmountAndDate = amountDifference == 0 && daysDifference == 0;
                var isCloseMatch = daysDifference <= 1 && amountDifference <= 0.01m;
                
                // Calculate confidence based primarily on amount/date match
                var dateConfidence = 1.0 - (daysDifference / options.DateToleranceDays);
                var amountConfidence = amountDifference == 0 ? 1.0 : (1.0 - (double)amountDifference / (double)options.AmountTolerance);
                var baseConfidence = (dateConfidence * 0.5 + amountConfidence * 0.5);
                
                // Description similarity adds bonus confidence but isn't required
                var confidence = (decimal)(baseConfidence * 0.8 + similarity * 0.2);
                
                // Flag as duplicate if amount/date match OR description matches threshold
                if (isExactAmountAndDate || isCloseMatch || similarity >= options.DescriptionSimilarityThreshold)
                {
                    var message = isExactAmountAndDate ? 
                        "Transaction with same amount and date found" :
                        $"Similar transaction found ({daysDifference} day{(daysDifference == 1 ? "" : "s")} apart)";
                    
                    if (similarity > 0)
                        message += $" - {similarity:P0} description match";
                    else if (string.IsNullOrWhiteSpace(candidate.Description) || string.IsNullOrWhiteSpace(existing.Description))
                        message += " - empty description";

                    conflicts.Add(new ConflictInfoDto
                    {
                        Type = ConflictType.PotentialDuplicate,
                        Severity = isExactAmountAndDate ? ConflictSeverity.High :
                                  confidence > 0.7m ? ConflictSeverity.Medium : ConflictSeverity.Low,
                        Message = message,
                        ConflictingTransaction = MapToExistingTransactionDto(existing),
                        ConfidenceScore = confidence
                    });
                }
            }

            // Check for transfer conflicts - if existing transaction is a transfer,
            // check if import candidate has same amount and similar date (potential duplicate)
            if (existing.IsTransfer() && daysDifference <= options.DateToleranceDays && 
                Math.Abs(candidate.Amount - existing.Amount) <= options.AmountTolerance)
            {
                var severity = amountDifference == 0 && daysDifference <= 1 
                    ? ConflictSeverity.High 
                    : ConflictSeverity.Medium;
                
                var confidence = amountDifference == 0 
                    ? (decimal)(1.0 - daysDifference / options.DateToleranceDays)
                    : 0.7m;

                conflicts.Add(new ConflictInfoDto
                {
                    Type = ConflictType.TransferConflict,
                    Severity = severity,
                    Message = $"Transfer with same amount already exists ({daysDifference} days apart)",
                    ConflictingTransaction = MapToExistingTransactionDto(existing),
                    ConfidenceScore = confidence
                });
            }
        }

        return conflicts;
    }

    private async Task<Transaction> CreateTransactionFromCandidateAsync(ImportCandidateDto candidate, int accountId)
    {
        // Debug logging
        _logger.LogDebug("CreateTransactionFromCandidate: amount={Amount}, candidateType={CandidateType}, description={Description}", 
            candidate.Amount, candidate.Type, candidate.Description);
        
        // Amount is already normalized in AnalyzeImportAsync() before duplicate detection
        // No need to normalize again here
        var transaction = new Transaction
        {
            AccountId = accountId,
            Amount = candidate.Amount,
            TransactionDate = candidate.Date,
            Description = candidate.Description,
            ReferenceNumber = candidate.ReferenceId,
            ExternalId = candidate.ExternalReferenceId,
            Status = candidate.Status,
            Type = candidate.Type,
            Source = TransactionSource.CsvImport, // or determine from context
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsReviewed = false
        };
        
        _logger.LogDebug("Transaction created with type: {TransactionType}, amount: {Amount}", 
            transaction.Type, transaction.Amount);

        // Save to database and return the saved entity
        var savedTransaction = await _transactionRepository.AddAsync(transaction);
        await _transactionRepository.SaveChangesAsync();
        
        return savedTransaction;
    }

    private async Task MergeTransactionAsync(ImportCandidateDto candidate, ExistingTransactionDto existingTransaction, Guid userId)
    {
        // Get the existing transaction from database
        var transaction = await _transactionRepository.GetByIdAsync(existingTransaction.Id, userId);
        if (transaction == null)
        {
            throw new InvalidOperationException($"Existing transaction {existingTransaction.Id} not found");
        }

        // Merge relevant data from candidate into existing transaction
        // Update fields that might be more accurate in the import
        if (!string.IsNullOrEmpty(candidate.ExternalReferenceId) && 
            string.IsNullOrEmpty(transaction.ExternalId))
        {
            transaction.ExternalId = candidate.ExternalReferenceId;
        }

        if (!string.IsNullOrEmpty(candidate.ReferenceId) && 
            string.IsNullOrEmpty(transaction.ReferenceNumber))
        {
            transaction.ReferenceNumber = candidate.ReferenceId;
        }

        // Mark as reviewed and add source information
        transaction.IsReviewed = true;
        transaction.UpdatedAt = DateTime.UtcNow;

        // Add a note about the merge in the description if needed
        if (!transaction.Description.Contains("(merged)"))
        {
            transaction.Description += " (merged from import)";
        }

        await _transactionRepository.UpdateAsync(transaction);
        await _transactionRepository.SaveChangesAsync();

        _logger.LogInformation("Merged import candidate with existing transaction {TransactionId}", transaction.Id);
    }

    private async Task ReplaceTransactionAsync(ImportCandidateDto candidate, ExistingTransactionDto existingTransaction, int accountId, Guid userId)
    {
        // Get the existing transaction from database
        var transaction = await _transactionRepository.GetByIdAsync(existingTransaction.Id, userId);
        if (transaction == null)
        {
            throw new InvalidOperationException($"Existing transaction {existingTransaction.Id} not found");
        }

        // Update the existing transaction with data from the candidate
        // Normalize amount based on transaction type
        transaction.Amount = candidate.Type == TransactionType.Expense
            ? -Math.Abs(candidate.Amount)  // Always negative for expenses
            : Math.Abs(candidate.Amount);  // Always positive for income
        transaction.TransactionDate = candidate.Date;
        transaction.Description = candidate.Description;
        transaction.ReferenceNumber = candidate.ReferenceId;
        transaction.ExternalId = candidate.ExternalReferenceId;
        transaction.Status = candidate.Status;
        transaction.Type = candidate.Type;
        transaction.Source = TransactionSource.CsvImport; // Update source to show it came from import
        transaction.IsReviewed = true;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _transactionRepository.UpdateAsync(transaction);
        await _transactionRepository.SaveChangesAsync();

        _logger.LogInformation("Replaced existing transaction {TransactionId} with import candidate data", transaction.Id);
    }

    private static double CalculateDescriptionSimilarity(string desc1, string desc2)
    {
        if (string.IsNullOrWhiteSpace(desc1) || string.IsNullOrWhiteSpace(desc2))
            return 0;

        desc1 = desc1.Trim().ToLowerInvariant();
        desc2 = desc2.Trim().ToLowerInvariant();

        if (desc1 == desc2) return 1.0;

        // Simple Jaccard similarity based on words
        var words1 = desc1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = desc2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private static ImportAnalysisStatistics CalculateStatistics(List<ImportReviewItemDto> reviewItems)
    {
        var cleanImports = reviewItems.Count(r => !r.Conflicts.Any());
        var exactDuplicates = reviewItems.Count(r => r.Conflicts.Any(c => c.Type == ConflictType.ExactDuplicate));
        var potentialDuplicates = reviewItems.Count(r => r.Conflicts.Any(c => c.Type == ConflictType.PotentialDuplicate));
        var transferConflicts = reviewItems.Count(r => r.Conflicts.Any(c => c.Type == ConflictType.TransferConflict));
        var manualConflicts = reviewItems.Count(r => r.Conflicts.Any(c => c.Type == ConflictType.ManualEntryConflict));
        var requiresReview = reviewItems.Count - cleanImports;

        return new ImportAnalysisStatistics
        {
            TotalCandidates = reviewItems.Count,
            CleanImports = cleanImports,
            ExactDuplicates = exactDuplicates,
            PotentialDuplicates = potentialDuplicates,
            TransferConflicts = transferConflicts,
            ManualConflicts = manualConflicts,
            RequiresReview = requiresReview
        };
    }

    private static List<string> GenerateAnalysisNotes(List<ImportReviewItemDto> reviewItems, ImportAnalysisOptions options)
    {
        var notes = new List<string>();

        var hasHighConfidenceConflicts = reviewItems.Any(r => 
            r.Conflicts.Any(c => c.ConfidenceScore > 0.9m));
        
        if (hasHighConfidenceConflicts)
        {
            notes.Add("High-confidence duplicate matches detected - review recommended");
        }

        var transferConflicts = reviewItems.Count(r => 
            r.Conflicts.Any(c => c.Type == ConflictType.TransferConflict));
        
        if (transferConflicts > 0)
        {
            notes.Add($"{transferConflicts} potential transfer conflicts detected");
        }

        if (options.DateToleranceDays > 5)
        {
            notes.Add("Large date tolerance may result in false positive matches");
        }

        return notes;
    }

    private static (List<string> warnings, List<string> errors) ExtractWarningsAndErrors(
        List<ImportReviewItemDto> reviewItems, 
        ImportAnalysisOptions options)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Check for potential data quality issues
        var duplicateExternalIds = reviewItems
            .Where(r => !string.IsNullOrEmpty(r.ImportCandidate.ExternalReferenceId))
            .GroupBy(r => r.ImportCandidate.ExternalReferenceId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateExternalIds.Any())
        {
            warnings.Add($"Duplicate external reference IDs found: {string.Join(", ", duplicateExternalIds)}");
        }

        // Check for missing essential data
        var missingDescriptions = reviewItems.Count(r => string.IsNullOrWhiteSpace(r.ImportCandidate.Description));
        if (missingDescriptions > 0)
        {
            warnings.Add($"{missingDescriptions} transactions have missing or empty descriptions");
        }

        // Check for very large amounts that might be data entry errors
        var largeAmounts = reviewItems.Where(r => Math.Abs(r.ImportCandidate.Amount) > 100000).ToList();
        if (largeAmounts.Any())
        {
            warnings.Add($"{largeAmounts.Count} transactions have amounts over $100,000 - please verify these are correct");
        }

        // Check for future-dated transactions
        var futureDatedCount = reviewItems.Count(r => r.ImportCandidate.Date > DateTime.UtcNow.AddDays(1));
        if (futureDatedCount > 0)
        {
            warnings.Add($"{futureDatedCount} transactions are dated in the future");
        }

        // Check for very old transactions
        var veryOldCount = reviewItems.Count(r => r.ImportCandidate.Date < DateTime.UtcNow.AddYears(-5));
        if (veryOldCount > 0)
        {
            warnings.Add($"{veryOldCount} transactions are older than 5 years");
        }

        // Check for potential tolerance setting issues
        if (options.DateToleranceDays > 7)
        {
            warnings.Add("Large date tolerance may result in false positive matches");
        }

        if (options.AmountTolerance > 1.0m)
        {
            warnings.Add("Large amount tolerance may result in false positive matches");
        }

        return (warnings, errors);
    }

    private static ExistingTransactionDto MapToExistingTransactionDto(Transaction transaction)
    {
        return new ExistingTransactionDto
        {
            Id = transaction.Id,
            Amount = transaction.Amount,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            ReferenceId = transaction.ReferenceNumber,
            ExternalReferenceId = transaction.ExternalId,
            Source = transaction.Source,
            Status = transaction.Status,
            CreatedAt = transaction.CreatedAt
        };
    }

    private static ImportedTransactionDto MapToImportedTransactionDto(Transaction transaction)
    {
        return new ImportedTransactionDto
        {
            Id = transaction.Id,
            Amount = transaction.Amount,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            ExternalId = transaction.ExternalId,
            IsNew = true,
            IsSkipped = false
        };
    }
}