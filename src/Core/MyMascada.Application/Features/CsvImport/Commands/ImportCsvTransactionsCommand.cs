using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.Transactions.Services;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.CsvImport.Commands;

public class ImportCsvTransactionsCommand : IRequest<CsvImportResponse>
{
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
    public byte[] CsvData { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public CsvFormat Format { get; set; } = CsvFormat.Generic;
    public bool HasHeader { get; set; } = true;
    public bool SkipDuplicates { get; set; } = true;
    public bool AutoCategorize { get; set; } = true;
}

public class ImportCsvTransactionsCommandHandler : IRequestHandler<ImportCsvTransactionsCommand, CsvImportResponse>
{
    private readonly ICsvImportService _csvImportService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly TransactionReviewService _reviewService;
    private readonly ICategorizationPipeline _categorizationPipeline;

    public ImportCsvTransactionsCommandHandler(
        ICsvImportService csvImportService,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        TransactionReviewService reviewService,
        ICategorizationPipeline categorizationPipeline)
    {
        _csvImportService = csvImportService;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _reviewService = reviewService;
        _categorizationPipeline = categorizationPipeline;
    }

    public async Task<CsvImportResponse> Handle(ImportCsvTransactionsCommand request, CancellationToken cancellationToken)
    {
        var response = new CsvImportResponse();

        try
        {
            // Validate account belongs to user
            var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
            if (account == null)
            {
                response.Errors.Add($"Account with ID {request.AccountId} not found or does not belong to user");
                return response;
            }

            // Create stream from byte array
            using var csvStream = new MemoryStream(request.CsvData);
            
            // Validate CSV file
            if (!await _csvImportService.ValidateFileAsync(csvStream))
            {
                response.Errors.Add("Invalid CSV file format");
                return response;
            }

            // Reset stream position after validation
            csvStream.Position = 0;

            // Get field mapping for the specified format
            var mapping = _csvImportService.GetDefaultMapping(request.Format);

            // Parse CSV
            var parseResult = await _csvImportService.ParseCsvAsync(csvStream, mapping, request.HasHeader);
            
            response.TotalRows = parseResult.TotalRows;
            response.Errors.AddRange(parseResult.Errors);
            response.Warnings.AddRange(parseResult.Warnings);

            if (!parseResult.IsSuccess || parseResult.Transactions.Count == 0)
            {
                response.Errors.Add("No valid transactions found in CSV file");
                return response;
            }

            // Process each transaction
            foreach (var csvTransaction in parseResult.Transactions)
            {
                try
                {
                    var importedTransaction = await ProcessTransaction(csvTransaction, request, account);
                    response.ImportedTransactions.Add(importedTransaction);

                    if (importedTransaction.IsNew)
                    {
                        response.ProcessedRows++;
                    }
                    else if (importedTransaction.IsSkipped)
                    {
                        response.SkippedRows++;
                    }
                }
                catch (Exception ex)
                {
                    response.ErrorRows++;
                    response.Errors.Add($"Row {csvTransaction.RowNumber}: {ex.Message}");
                }
            }

            response.IsSuccess = response.ProcessedRows > 0 || response.SkippedRows > 0;
            
            // Apply batch categorization if auto-categorization is enabled
            if (request.AutoCategorize && response.ProcessedRows > 0)
            {
                await ApplyBatchCategorization(response, request.UserId, cancellationToken);
            }
            
            if (response.ProcessedRows > 0)
            {
                response.Warnings.Add($"Successfully imported {response.ProcessedRows} transactions");
            }
            
            if (response.SkippedRows > 0)
            {
                response.Warnings.Add($"Skipped {response.SkippedRows} duplicate transactions");
            }
        }
        catch (Exception ex)
        {
            response.Errors.Add($"Import failed: {ex.Message}");
            response.IsSuccess = false;
        }

        return response;
    }

    private async Task<ImportedTransactionDto> ProcessTransaction(
        CsvTransactionRow csvTransaction, 
        ImportCsvTransactionsCommand request, 
        Account account)
    {
        var importedTransaction = new ImportedTransactionDto
        {
            Amount = csvTransaction.Amount,
            TransactionDate = csvTransaction.Date,
            Description = csvTransaction.Description,
            ExternalId = csvTransaction.ExternalId
        };

        // Check for duplicates if requested
        if (request.SkipDuplicates && !string.IsNullOrEmpty(csvTransaction.ExternalId))
        {
            var existingTransaction = await _transactionRepository.ExistsByExternalIdAsync(
                csvTransaction.ExternalId, request.AccountId);
            
            if (existingTransaction)
            {
                importedTransaction.IsSkipped = true;
                importedTransaction.SkipReason = "Duplicate transaction (same external ID)";
                return importedTransaction;
            }
        }

        // Create new transaction
        var transaction = new Transaction
        {
            Amount = csvTransaction.Amount,
            TransactionDate = csvTransaction.Date,
            Description = csvTransaction.Description,
            UserDescription = null,
            Status = csvTransaction.Status,
            Source = TransactionSource.CsvImport,
            ExternalId = csvTransaction.ExternalId,
            ReferenceNumber = csvTransaction.Reference,
            Notes = csvTransaction.Notes,
            Location = null,
            IsReviewed = true, // Default to reviewed - will be overridden if flagged
            IsExcluded = false,
            Tags = null,
            AccountId = request.AccountId,
            CategoryId = null, // Will be set by batch categorization if enabled
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };
        
        // Set transaction type based on amount
        transaction.SetTypeFromAmount();

        // Save transaction first
        var savedTransaction = await _transactionRepository.AddAsync(transaction);
        
        // Check if transaction should be flagged for review using smart flagging
        var shouldFlag = await _reviewService.ShouldFlagForReview(savedTransaction, request.UserId);
        if (shouldFlag)
        {
            savedTransaction.IsReviewed = false;
            await _transactionRepository.UpdateAsync(savedTransaction);
        }
        
        importedTransaction.Id = savedTransaction.Id;
        importedTransaction.IsNew = true;

        return importedTransaction;
    }

    private async Task ApplyBatchCategorization(CsvImportResponse response, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // Get the newly imported transactions that need categorization
            var transactionIds = response.ImportedTransactions
                .Where(t => t.IsNew && t.Id != 0)
                .Select(t => t.Id)
                .ToList();

            if (!transactionIds.Any())
                return;

            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, userId, cancellationToken);
            var uncategorizedTransactions = transactions.Where(t => !t.CategoryId.HasValue).ToList();

            if (!uncategorizedTransactions.Any())
                return;

            // Apply categorization pipeline
            var result = await _categorizationPipeline.ProcessAsync(uncategorizedTransactions, cancellationToken);

            // Apply categorizations to the database
            var categorizedCount = 0;
            foreach (var categorizedTransaction in result.CategorizedTransactions)
            {
                categorizedTransaction.Transaction.CategoryId = categorizedTransaction.CategoryId;
                await _transactionRepository.UpdateAsync(categorizedTransaction.Transaction);
                categorizedCount++;
            }

            if (categorizedCount > 0)
            {
                response.Warnings.Add($"Auto-categorized {categorizedCount} transactions using rules and AI");
                
                // Add categorization metrics to response
                if (result.Metrics.ProcessedByRules > 0)
                    response.Warnings.Add($"  - {result.Metrics.ProcessedByRules} categorized by rules");
                if (result.Metrics.ProcessedByML > 0)
                    response.Warnings.Add($"  - {result.Metrics.ProcessedByML} categorized by ML");
                if (result.Metrics.ProcessedByLLM > 0)
                    response.Warnings.Add($"  - {result.Metrics.ProcessedByLLM} categorized by AI");
                if (result.Metrics.EstimatedCostSavings > 0)
                    response.Warnings.Add($"  - Estimated cost savings: ${result.Metrics.EstimatedCostSavings:F4}");
            }
        }
        catch (Exception ex)
        {
            // Don't fail the import if categorization fails
            response.Warnings.Add($"Auto-categorization partially failed: {ex.Message}");
        }
    }
}