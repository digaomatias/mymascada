using System.Text.Json;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using ProviderBankTransactionDto = MyMascada.Application.Features.BankConnections.DTOs.BankTransactionDto;

namespace MyMascada.Application.Features.Reconciliation.Commands;

/// <summary>
/// Command to create a reconciliation from Akahu bank data.
/// Fetches transactions from Akahu, performs matching, and returns results.
/// </summary>
public record CreateAkahuReconciliationCommand : IRequest<AkahuReconciliationResponse>
{
    public Guid UserId { get; init; }
    public int AccountId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal? StatementEndBalance { get; init; }
    public string? Notes { get; init; }
}

public class CreateAkahuReconciliationCommandHandler
    : IRequestHandler<CreateAkahuReconciliationCommand, AkahuReconciliationResponse>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankProviderFactory _bankProviderFactory;
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITransactionMatchingService _matchingService;
    private readonly IApplicationLogger<CreateAkahuReconciliationCommandHandler> _logger;

    private readonly IAccountAccessService _accountAccessService;

    public CreateAkahuReconciliationCommandHandler(
        IAccountRepository accountRepository,
        IBankConnectionRepository bankConnectionRepository,
        IBankProviderFactory bankProviderFactory,
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        IReconciliationAuditLogRepository auditLogRepository,
        ITransactionRepository transactionRepository,
        ITransactionMatchingService matchingService,
        IAccountAccessService accountAccessService,
        IApplicationLogger<CreateAkahuReconciliationCommandHandler> logger)
    {
        _accountRepository = accountRepository;
        _bankConnectionRepository = bankConnectionRepository;
        _bankProviderFactory = bankProviderFactory;
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _auditLogRepository = auditLogRepository;
        _transactionRepository = transactionRepository;
        _matchingService = matchingService;
        _accountAccessService = accountAccessService;
        _logger = logger;
    }

    public async Task<AkahuReconciliationResponse> Handle(
        CreateAkahuReconciliationCommand request,
        CancellationToken cancellationToken)
    {
        // Validate account exists and belongs to user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
        {
            throw new ArgumentException($"Account with ID {request.AccountId} not found or does not belong to user");
        }

        // Verify the user has modify permission on this account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, request.AccountId))
        {
            throw new UnauthorizedAccessException("You do not have permission to create reconciliations on this account.");
        }

        // Get the bank connection
        var bankConnection = await _bankConnectionRepository.GetByAccountIdAsync(
            request.AccountId,
            cancellationToken);

        if (bankConnection == null)
        {
            throw new InvalidOperationException("No bank connection found for this account");
        }

        if (!string.Equals(bankConnection.ProviderId, "akahu", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Bank connection uses provider '{bankConnection.ProviderId}', not Akahu");
        }

        // Get the Akahu provider
        var provider = _bankProviderFactory.GetProviderOrDefault("akahu");
        if (provider == null)
        {
            throw new InvalidOperationException("Akahu provider is not available");
        }

        // Normalize dates to UTC for PostgreSQL compatibility
        var startDateUtc = request.StartDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc)
            : request.StartDate.ToUniversalTime();
        var endDateUtc = request.EndDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc)
            : request.EndDate.ToUniversalTime();

        // Build the connection config
        var connectionConfig = BuildConnectionConfig(bankConnection);

        // Fetch transactions from Akahu
        _logger.LogInformation(
            "Fetching transactions from Akahu for account {AccountId} ({StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd})",
            request.AccountId, startDateUtc, endDateUtc);

        var fetchResult = await provider.FetchTransactionsAsync(
            connectionConfig,
            startDateUtc,
            endDateUtc,
            cancellationToken);

        if (!fetchResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to fetch transactions from Akahu: {fetchResult.ErrorMessage}");
        }

        _logger.LogInformation(
            "Fetched {Count} transactions from Akahu for account {AccountId}",
            fetchResult.Transactions.Count, request.AccountId);

        // Fetch balance from Akahu (for comparison)
        AkahuBalanceComparisonDto? balanceComparison = null;
        if (provider.SupportsBalanceFetch)
        {
            var balanceResult = await provider.FetchBalanceAsync(connectionConfig, cancellationToken);
            if (balanceResult?.IsSuccess == true)
            {
                // Fetch pending transactions to adjust the balance.
                // Akahu's current balance includes pending transactions, but only cleared
                // transactions are available for matching, so we subtract pending to get
                // a comparable "cleared balance".
                var pendingSummary = await provider.FetchPendingTransactionsSummaryAsync(connectionConfig, cancellationToken);
                var adjustedAkahuBalance = balanceResult.CurrentBalance - pendingSummary.Total;

                var myMascadaBalance = await _transactionRepository.GetAccountBalanceAsync(
                    request.AccountId,
                    request.UserId);

                balanceComparison = new AkahuBalanceComparisonDto
                {
                    AkahuBalance = adjustedAkahuBalance,
                    MyMascadaBalance = myMascadaBalance,
                    Difference = adjustedAkahuBalance - myMascadaBalance,
                    IsBalanced = Math.Abs(adjustedAkahuBalance - myMascadaBalance) <= 0.01m,
                    IsCurrentBalance = true,
                    PendingTransactionsTotal = pendingSummary.Total,
                    PendingTransactionsCount = pendingSummary.Count
                };
            }
        }

        // Determine statement end balance
        var statementEndBalance = request.StatementEndBalance
            ?? balanceComparison?.AkahuBalance
            ?? await _transactionRepository.GetAccountBalanceAsync(request.AccountId, request.UserId);

        // Calculate current balance
        var calculatedBalance = await _transactionRepository.GetAccountBalanceAsync(
            request.AccountId,
            request.UserId);

        // Create the reconciliation entity
        var reconciliation = new Domain.Entities.Reconciliation
        {
            AccountId = request.AccountId,
            ReconciliationDate = DateTime.UtcNow,
            StatementEndDate = endDateUtc,
            StatementEndBalance = statementEndBalance,
            CalculatedBalance = calculatedBalance,
            Status = ReconciliationStatus.InProgress,
            CreatedByUserId = request.UserId,
            Notes = request.Notes ?? "Reconciliation from Akahu bank data",
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        var savedReconciliation = await _reconciliationRepository.AddAsync(reconciliation);

        // Map Akahu transactions to reconciliation format
        var reconciliationBankTransactions = AkahuTransactionMapper
            .MapToReconciliationFormat(fetchResult.Transactions)
            .ToList();

        // Get app transactions for the account within the date range
        // Include already-reconciled transactions so they can match against bank transactions
        // and avoid being incorrectly recommended as new imports
        var accountTransactions = await _transactionRepository.GetByDateRangeAsync(
            request.UserId,
            request.AccountId,
            startDateUtc,
            endDateUtc);

        // Perform matching
        var matchingRequest = new TransactionMatchRequest
        {
            ReconciliationId = savedReconciliation.Id,
            BankTransactions = reconciliationBankTransactions,
            StartDate = startDateUtc,
            EndDate = endDateUtc,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 2
        };

        var matchingResult = await _matchingService.MatchTransactionsAsync(
            matchingRequest,
            accountTransactions);

        // Create reconciliation items from matching results
        var reconciliationItems = new List<ReconciliationItem>();

        // Add matched pairs
        foreach (var matchedPair in matchingResult.MatchedPairs)
        {
            var item = new ReconciliationItem
            {
                ReconciliationId = savedReconciliation.Id,
                TransactionId = matchedPair.AppTransaction.Id,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = matchedPair.MatchConfidence,
                MatchMethod = matchedPair.MatchMethod,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            // Store full Akahu transaction data for potential import
            var providerTransaction = fetchResult.Transactions
                .FirstOrDefault(t => t.ExternalId == matchedPair.BankTransaction.BankTransactionId);

            if (providerTransaction != null)
            {
                var referenceData = AkahuTransactionMapper.CreateBankReferenceData(providerTransaction);
                item.SetBankReferenceData(referenceData);
            }
            else
            {
                item.SetBankReferenceData(matchedPair.BankTransaction);
            }

            reconciliationItems.Add(item);
        }

        // Add unmatched app transactions
        foreach (var unmatchedApp in matchingResult.UnmatchedAppTransactions)
        {
            var item = new ReconciliationItem
            {
                ReconciliationId = savedReconciliation.Id,
                TransactionId = unmatchedApp.Id,
                ItemType = ReconciliationItemType.UnmatchedApp,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            reconciliationItems.Add(item);
        }

        // Add unmatched bank transactions (with full Akahu data for import)
        foreach (var unmatchedBank in matchingResult.UnmatchedBankTransactions)
        {
            var item = new ReconciliationItem
            {
                ReconciliationId = savedReconciliation.Id,
                ItemType = ReconciliationItemType.UnmatchedBank,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            // Store full Akahu transaction data for potential import
            var providerTransaction = fetchResult.Transactions
                .FirstOrDefault(t => t.ExternalId == unmatchedBank.BankTransactionId);

            if (providerTransaction != null)
            {
                var referenceData = AkahuTransactionMapper.CreateBankReferenceData(providerTransaction);
                item.SetBankReferenceData(referenceData);
            }
            else
            {
                item.SetBankReferenceData(unmatchedBank);
            }

            reconciliationItems.Add(item);
        }

        // Save all reconciliation items
        await _reconciliationItemRepository.AddRangeAsync(reconciliationItems);

        // Create audit log entry
        var auditLog = new ReconciliationAuditLog
        {
            ReconciliationId = savedReconciliation.Id,
            Action = ReconciliationAction.BankStatementImported,
            UserId = request.UserId,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        auditLog.SetDetails(new
        {
            Source = "Akahu",
            BankTransactionCount = fetchResult.Transactions.Count,
            AppTransactionCount = accountTransactions.Count(),
            ExactMatches = matchingResult.ExactMatches,
            FuzzyMatches = matchingResult.FuzzyMatches,
            UnmatchedBank = matchingResult.UnmatchedBank,
            UnmatchedApp = matchingResult.UnmatchedApp,
            OverallMatchPercentage = matchingResult.OverallMatchPercentage,
            BalanceComparison = balanceComparison
        });

        await _auditLogRepository.AddAsync(auditLog);

        _logger.LogInformation(
            "Created Akahu reconciliation {ReconciliationId} for account {AccountId}: " +
            "{ExactMatches} exact, {FuzzyMatches} fuzzy, {UnmatchedBank} unmatched bank, {UnmatchedApp} unmatched app",
            savedReconciliation.Id,
            request.AccountId,
            matchingResult.ExactMatches,
            matchingResult.FuzzyMatches,
            matchingResult.UnmatchedBank,
            matchingResult.UnmatchedApp);

        return new AkahuReconciliationResponse
        {
            ReconciliationId = savedReconciliation.Id,
            MatchingResult = matchingResult,
            BalanceComparison = balanceComparison
        };
    }

    private static BankConnectionConfig BuildConnectionConfig(BankConnection connection)
    {
        var settings = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(connection.EncryptedSettings))
        {
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
}
