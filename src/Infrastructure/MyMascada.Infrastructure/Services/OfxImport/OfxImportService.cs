using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.OfxImport.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.OfxImport;

public class OfxImportService : IOfxImportService
{
    private readonly OfxParserService _parserService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;

    public OfxImportService(
        OfxParserService parserService, 
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository)
    {
        _parserService = parserService;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<OfxImportResponse> ImportOfxFileAsync(OfxImportRequest request, string userId)
    {
        var response = new OfxImportResponse();
        var userGuid = Guid.Parse(userId);

        try
        {
            // Parse the OFX file
            var parseResult = _parserService.ParseOfxFile(request.Content);
            if (!parseResult.Success)
            {
                response.Success = false;
                response.Message = parseResult.Message;
                response.Errors.AddRange(parseResult.Errors);
                response.Warnings.AddRange(parseResult.Warnings);
                return response;
            }

            // Get or create account
            Account? account = null;
            if (request.AccountId.HasValue)
            {
                account = await _accountRepository.GetByIdAsync(request.AccountId.Value, userGuid);
                if (account == null)
                {
                    response.Success = false;
                    response.Message = $"Account with ID {request.AccountId} not found";
                    response.Errors.Add($"Account with ID {request.AccountId} not found");
                    return response;
                }
            }
            else if (request.CreateAccountIfNotExists)
            {
                // Create new account based on OFX data or user input
                string accountName;
                AccountType accountType = AccountType.Checking; // Default
                string? institution = null;
                string? lastFourDigits = null;

                if (parseResult.AccountInfo != null)
                {
                    // Use OFX account information if available
                    accountName = !string.IsNullOrEmpty(request.AccountName) 
                        ? request.AccountName 
                        : $"{parseResult.AccountInfo.AccountType} - {parseResult.AccountInfo.AccountId}";
                    accountType = MapOfxAccountType(parseResult.AccountInfo.AccountType);
                    institution = parseResult.AccountInfo.BankId;
                    // Safely extract last 4 digits
                    if (!string.IsNullOrEmpty(parseResult.AccountInfo.AccountId))
                    {
                        var accountId = parseResult.AccountInfo.AccountId.Trim();
                        lastFourDigits = accountId.Length > 4 
                            ? accountId.Substring(accountId.Length - 4)
                            : accountId;
                    }
                }
                else
                {
                    // Use user-provided account name if OFX info not available
                    accountName = !string.IsNullOrEmpty(request.AccountName) 
                        ? request.AccountName 
                        : "Imported Account";
                }

                account = new Account
                {
                    Name = accountName,
                    Type = accountType,
                    Institution = institution,
                    LastFourDigits = lastFourDigits,
                    Currency = "USD", // Default to USD, could be extracted from OFX
                    UserId = userGuid,
                    CurrentBalance = 0 // Will be updated as transactions are imported
                };

                account = await _accountRepository.AddAsync(account);
            }

            if (account == null)
            {
                response.Success = false;
                response.Message = "No account specified and account creation not enabled";
                response.Errors.Add("No account specified and account creation not enabled");
                return response;
            }

            // Import transactions
            foreach (var ofxTransaction in parseResult.Transactions)
            {
                // Check for duplicates using external ID (scoped to current user)
                if (!string.IsNullOrEmpty(ofxTransaction.TransactionId))
                {
                    var existingTransaction = await _transactionRepository.GetTransactionByExternalIdAsync(userId, ofxTransaction.TransactionId);
                    if (existingTransaction != null)
                    {
                        response.DuplicateTransactionsCount++;
                        continue;
                    }
                }

                // Create transaction
                var transaction = new Transaction
                {
                    Amount = ofxTransaction.Amount,
                    TransactionDate = EnsureUtcDateTime(ofxTransaction.PostedDate),
                    Description = ofxTransaction.Name,
                    ExternalId = ofxTransaction.TransactionId,
                    AccountId = account.Id,
                    Status = TransactionStatus.Cleared,
                    Source = TransactionSource.OfxImport,
                    Notes = ofxTransaction.Memo
                };

                try
                {
                    await _transactionRepository.AddAsync(transaction);
                    response.ImportedTransactionsCount++;
                    
                    // Update account balance
                    account.CurrentBalance += transaction.Amount;
                }
                catch
                {
                    response.SkippedTransactionsCount++;
                    response.Errors.Add($"Failed to import transaction: {ofxTransaction.Name}");
                }
            }

            // Update account with final balance
            await _accountRepository.UpdateAsync(account);
            await _transactionRepository.SaveChangesAsync();

            response.Success = true;
            response.Message = $"Successfully imported {response.ImportedTransactionsCount} transactions";
            response.Warnings.AddRange(parseResult.Warnings);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = "An error occurred during import";
            response.Errors.Add(ex.Message);
        }

        return response;
    }

    private static AccountType MapOfxAccountType(string ofxAccountType)
    {
        return ofxAccountType?.ToUpper() switch
        {
            "CHECKING" => AccountType.Checking,
            "SAVINGS" => AccountType.Savings,
            "CREDITCARD" => AccountType.CreditCard,
            "INVESTMENT" => AccountType.Investment,
            "LOAN" => AccountType.Loan,
            _ => AccountType.Checking // Default to checking
        };
    }

    /// <summary>
    /// Ensures DateTime is in UTC format for PostgreSQL compatibility
    /// </summary>
    private static DateTime EnsureUtcDateTime(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }
}