using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Events;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.CsvImport.Commands;

/// <summary>
/// Handler for importing CSV with AI-suggested column mappings
/// </summary>
public class ImportCsvWithMappingsCommandHandler : IRequestHandler<ImportCsvWithMappingsCommand, CsvImportResponse>
{
    private readonly ICsvParserService _csvParserService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<ImportCsvWithMappingsCommandHandler> _logger;

    public ImportCsvWithMappingsCommandHandler(
        ICsvParserService csvParserService,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        IMediator mediator,
        ILogger<ImportCsvWithMappingsCommandHandler> logger)
    {
        _csvParserService = csvParserService;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CsvImportResponse> Handle(ImportCsvWithMappingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting CSV import with AI mappings for user {UserId}", request.UserId);

            // Parse CSV and get headers first to map column names to indices
            using var csvStream = new MemoryStream(request.CsvData);
            
            // Get CSV headers first
            csvStream.Position = 0;
            var reader = new StreamReader(csvStream);
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
            {
                return new CsvImportResponse
                {
                    IsSuccess = false,
                    Message = "CSV file appears to be empty or invalid"
                };
            }
            
            var headers = headerLine.Split(',').Select(h => h.Trim('"').Trim()).ToList();
            
            // Convert mappings to CsvFieldMapping format using actual headers
            var fieldMapping = ConvertMappings(request.Mappings, headers);

            // Reset stream and parse CSV using the converted mappings
            csvStream.Position = 0;
            var parseResult = await _csvParserService.ParseCsvAsync(csvStream, fieldMapping, true, request.MaxRows);

            _logger.LogInformation("CSV parsing completed. Success: {IsSuccess}, Transactions: {TransactionCount}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                parseResult.IsSuccess, parseResult.Transactions.Count, parseResult.Errors.Count, parseResult.Warnings.Count);

            if (!parseResult.IsSuccess)
            {
                return new CsvImportResponse
                {
                    IsSuccess = false,
                    Message = "Failed to parse CSV file",
                    Errors = parseResult.Errors,
                    Warnings = parseResult.Warnings
                };
            }

            // Log if no transactions were parsed
            if (parseResult.Transactions.Count == 0)
            {
                _logger.LogWarning("No transactions were parsed from CSV. This may indicate a column mapping issue.");
            }

            // Determine target account
            Account? targetAccount = null;
            if (request.AccountId.HasValue)
            {
                targetAccount = await _accountRepository.GetByIdAsync(request.AccountId.Value, request.UserId);
                if (targetAccount == null)
                {
                    return new CsvImportResponse
                    {
                        IsSuccess = false,
                        Message = "Account not found or access denied"
                    };
                }
            }
            else if (!string.IsNullOrEmpty(request.AccountName))
            {
                // Create new account
                targetAccount = new Account
                {
                    Name = request.AccountName,
                    Type = AccountType.Checking, // Default type
                    Currency = "USD", // Default currency - transactions inherit from account
                    CurrentBalance = 0,
                    CreatedBy = request.UserId.ToString(),
                    UpdatedBy = request.UserId.ToString(),
                    CreatedAt = DateTimeProvider.UtcNow,
                    UpdatedAt = DateTimeProvider.UtcNow
                };

                await _accountRepository.AddAsync(targetAccount);
            }
            else
            {
                return new CsvImportResponse
                {
                    IsSuccess = false,
                    Message = "Either AccountId or AccountName must be provided"
                };
            }

            // Convert parsed rows to transactions
            var transactions = new List<Transaction>();
            var skippedCount = 0;
            var duplicateCount = 0;

            foreach (var row in parseResult.Transactions)
            {
                try
                {
                    _logger.LogDebug("Processing transaction row {RowNumber}: Date={Date}, Amount={Amount}, Description={Description}", 
                        row.RowNumber, row.Date, row.Amount, row.Description);

                    // Check for duplicates if requested
                    if (request.SkipDuplicates)
                    {
                        var isDuplicate = await CheckForDuplicate(row, targetAccount.Id, request.UserId);
                        if (isDuplicate)
                        {
                            _logger.LogDebug("Skipping duplicate transaction: {Description}, Amount: {Amount}", row.Description, row.Amount);
                            duplicateCount++;
                            continue;
                        }
                    }

                    var transaction = CreateTransaction(row, targetAccount, request.UserId, request.Mappings);
                    transactions.Add(transaction);
                    _logger.LogDebug("Successfully created transaction: {Description}, Amount: {Amount}", transaction.Description, transaction.Amount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create transaction from row {RowNumber}: Date={Date}, Amount={Amount}, Description={Description}", 
                        row.RowNumber, row.Date, row.Amount, row.Description);
                    skippedCount++;
                }
            }

            // Save transactions one by one (no AddRangeAsync available)
            if (transactions.Any())
            {
                foreach (var transaction in transactions)
                {
                    await _transactionRepository.AddAsync(transaction);
                }

                // Publish event for asynchronous categorization processing
                if (request.AutoCategorize)
                {
                    try
                    {
                        var transactionIds = transactions.Select(t => t.Id).ToList();
                        var transactionsCreatedEvent = new TransactionsCreatedEvent(transactionIds, request.UserId);
                        await _mediator.Publish(transactionsCreatedEvent, cancellationToken);
                        
                        _logger.LogInformation("Published TransactionsCreatedEvent for {TransactionCount} transactions for async categorization", 
                            transactionIds.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish TransactionsCreatedEvent for user {UserId}. Categorization will need to be done manually.", 
                            request.UserId);
                        // Don't fail the import if event publishing fails
                    }
                }
            }

            var response = new CsvImportResponse
            {
                IsSuccess = true,
                Message = $"Successfully imported {transactions.Count} transactions",
                ImportedTransactionsCount = transactions.Count,
                SkippedTransactionsCount = skippedCount,
                DuplicateTransactionsCount = duplicateCount,
                Warnings = parseResult.Warnings,
                CreatedAccountId = request.AccountId.HasValue ? null : targetAccount.Id
            };

            _logger.LogInformation("CSV import completed. Imported: {ImportedCount}, Skipped: {SkippedCount}, Duplicates: {DuplicateCount}",
                transactions.Count, skippedCount, duplicateCount);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV import with mappings");
            return new CsvImportResponse
            {
                IsSuccess = false,
                Message = "An unexpected error occurred during import",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private CsvFieldMapping ConvertMappings(CsvColumnMappings mappings, List<string> headers)
    {
        // Convert AI mappings to the format expected by the CSV parser
        var fieldMapping = new CsvFieldMapping
        {
            DateFormat = mappings.DateFormat,
            IsAmountPositiveForDebits = mappings.AmountConvention == "type-column"
        };

        // Map column names to actual indices in the CSV
        if (!string.IsNullOrEmpty(mappings.DateColumn))
        {
            fieldMapping.DateColumn = GetColumnIndex(headers, mappings.DateColumn);
        }
        
        if (!string.IsNullOrEmpty(mappings.AmountColumn))
        {
            fieldMapping.AmountColumn = GetColumnIndex(headers, mappings.AmountColumn);
        }
        
        if (!string.IsNullOrEmpty(mappings.DescriptionColumn))
        {
            fieldMapping.DescriptionColumn = GetColumnIndex(headers, mappings.DescriptionColumn);
        }
        
        if (!string.IsNullOrEmpty(mappings.ReferenceColumn))
        {
            fieldMapping.ReferenceColumn = GetColumnIndex(headers, mappings.ReferenceColumn);
        }
        
        if (!string.IsNullOrEmpty(mappings.CategoryColumn))
        {
            fieldMapping.CategoryColumn = GetColumnIndex(headers, mappings.CategoryColumn);
        }

        if (!string.IsNullOrEmpty(mappings.TypeColumn))
        {
            fieldMapping.TypeColumn = GetColumnIndex(headers, mappings.TypeColumn);
        }

        _logger.LogDebug("Converted mappings: Date={DateColumn}, Amount={AmountColumn}, Description={DescriptionColumn}, Type={TypeColumn}, Headers={Headers}",
            fieldMapping.DateColumn, fieldMapping.AmountColumn, fieldMapping.DescriptionColumn, fieldMapping.TypeColumn, string.Join(",", headers));

        // Validate that required columns were found
        if (fieldMapping.DateColumn == -1 || fieldMapping.AmountColumn == -1 || fieldMapping.DescriptionColumn == -1)
        {
            var missingColumns = new List<string>();
            if (fieldMapping.DateColumn == -1) missingColumns.Add($"Date ('{mappings.DateColumn}')");
            if (fieldMapping.AmountColumn == -1) missingColumns.Add($"Amount ('{mappings.AmountColumn}')");
            if (fieldMapping.DescriptionColumn == -1) missingColumns.Add($"Description ('{mappings.DescriptionColumn}')");
            
            _logger.LogError("Required columns not found in CSV: {MissingColumns}", string.Join(", ", missingColumns));
        }

        return fieldMapping;
    }

    private int GetColumnIndex(List<string> headers, string columnName)
    {
        // Find exact match first
        var index = headers.FindIndex(h => h.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            return index;
        }

        // Try partial match if exact match fails
        index = headers.FindIndex(h => h.Contains(columnName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _logger.LogWarning("Using partial match for column '{ColumnName}' -> '{ActualHeader}' at index {Index}",
                columnName, headers[index], index);
            return index;
        }

        _logger.LogError("Column '{ColumnName}' not found in headers: {Headers}", columnName, string.Join(",", headers));
        return -1; // Column not found - this will cause parsing to skip this field
    }

    private Transaction CreateTransaction(
        CsvTransactionRow row, 
        Account account, 
        Guid userId,
        CsvColumnMappings mappings)
    {
        // Determine if this is income or expense based on amount convention
        var isIncome = DetermineTransactionDirection(row, mappings);
        
        // Amount should be signed: positive for income, negative for expenses
        var signedAmount = isIncome ? Math.Abs(row.Amount) : -Math.Abs(row.Amount);
        
        var transaction = new Transaction
        {
            Amount = signedAmount, // Store with correct sign: positive for income, negative for expenses
            TransactionDate = DateTimeProvider.ToUtc(row.Date),
            Description = row.Description,
            AccountId = account.Id,
            CreatedBy = userId.ToString(),
            UpdatedBy = userId.ToString(),
            Status = row.Status,
            Source = TransactionSource.CsvImport,
            ExternalId = row.ExternalId,
            ReferenceNumber = row.Reference,
            Notes = $"Imported from CSV using AI mapping",
            CreatedAt = DateTimeProvider.UtcNow,
            UpdatedAt = DateTimeProvider.UtcNow,
            Type = isIncome ? TransactionType.Income : TransactionType.Expense
        };

        return transaction;
    }

    private bool DetermineTransactionDirection(CsvTransactionRow row, CsvColumnMappings mappings)
    {
        return mappings.AmountConvention switch
        {
            "negative-expense" => row.Amount > 0, // Positive amounts are income, negative are expenses
            "negative-debits" => row.Amount > 0, // Legacy: same as negative-expense
            "positive-expense" => row.Amount < 0, // Positive amounts are expenses, negative are income (credit cards)
            "type-column" => DetermineFromTypeColumn(row, mappings),
            "all-positive-income" => true, // Legacy: All amounts are income
            "all-positive-expense" => false, // Legacy: All amounts are expenses
            "all-positive" => false, // Legacy: Default to expense for all-positive amounts
            _ => row.Amount > 0
        };
    }

    private bool DetermineFromTypeColumn(CsvTransactionRow row, CsvColumnMappings mappings)
    {
        // Check if we have a type column value
        if (!string.IsNullOrEmpty(row.Type))
        {
            // Use custom type mappings if available
            if (mappings.TypeValueMappings != null)
            {
                if (mappings.TypeValueMappings.IncomeValues.Contains(row.Type))
                {
                    return true; // Income
                }
                
                if (mappings.TypeValueMappings.ExpenseValues.Contains(row.Type))
                {
                    return false; // Expense
                }
                
                // If type value not in custom mappings, fall back to amount sign
                return row.Amount > 0;
            }
            
            // Legacy: use hardcoded patterns for backwards compatibility
            var typeValue = row.Type.ToLower();
            // Common patterns for income/credit transactions
            if (typeValue.Contains("credit") || typeValue.Contains("deposit") || 
                typeValue.Contains("income") || typeValue.Contains("pay") ||
                typeValue.Contains("refund") || typeValue.Contains("transfer in"))
            {
                return true; // Income
            }
            
            // Common patterns for expense/debit transactions
            if (typeValue.Contains("debit") || typeValue.Contains("withdrawal") || 
                typeValue.Contains("expense") || typeValue.Contains("payment") ||
                typeValue.Contains("purchase") || typeValue.Contains("transfer out"))
            {
                return false; // Expense
            }
        }
        
        // Fall back to amount sign if type column doesn't give clear indication
        return row.Amount > 0;
    }

    private async Task<bool> CheckForDuplicate(CsvTransactionRow row, int accountId, Guid userId)
    {
        // Check for existing transaction with same external ID
        if (!string.IsNullOrEmpty(row.ExternalId))
        {
            var existingByExternalId = await _transactionRepository
                .GetByExternalIdAsync(row.ExternalId);
            
            if (existingByExternalId != null)
            {
                _logger.LogDebug("Found duplicate by external ID: {ExternalId}", row.ExternalId);
                return true;
            }
        }

        // Check for potential duplicates based on amount, date, and account
        var toleranceAmount = 0.01m;
        var dateRange = TimeSpan.FromDays(1);
        
        // Ensure dates are UTC for PostgreSQL
        var startDate = DateTimeProvider.AddDaysUtc(row.Date, -dateRange.Days);
        var endDate = DateTimeProvider.AddDaysUtc(row.Date, dateRange.Days);
        
        var accountTransactions = await _transactionRepository
            .GetByDateRangeAsync(
                userId,
                accountId,
                startDate,
                endDate);
        
        _logger.LogDebug("Checking duplicates for row: Amount={Amount}, Date={Date}, Description={Description}. Found {Count} existing transactions in date range.",
            row.Amount, row.Date, row.Description, accountTransactions.Count());

        var isDuplicate = accountTransactions.Any(t => 
            Math.Abs(Math.Abs(t.Amount) - Math.Abs(row.Amount)) <= toleranceAmount &&
            t.Description.Equals(row.Description, StringComparison.OrdinalIgnoreCase) &&
            DateTimeProvider.AreSameDate(t.TransactionDate, row.Date));

        if (isDuplicate)
        {
            _logger.LogDebug("Found duplicate transaction: {Description}, Amount: {Amount}", row.Description, row.Amount);
        }

        return isDuplicate;
    }
}
