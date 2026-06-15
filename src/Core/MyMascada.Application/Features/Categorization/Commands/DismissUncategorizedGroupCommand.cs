using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Categorization.Commands;

public enum DismissUncategorizedGroupMode
{
    /// <summary>
    /// Hide the transactions from the Quick-Categorize wizard for 30 days.
    /// They reappear automatically after the snooze window — useful when the
    /// user wants to defer a decision without losing the prompt forever.
    /// </summary>
    Snooze = 0,

    /// <summary>
    /// Hide the transactions from the Quick-Categorize wizard permanently
    /// (until manually cleared elsewhere). Used for "Ignore" and
    /// "Mark as transfer" — cases where the wizard is the wrong surface.
    /// </summary>
    Ignore = 1,
}

/// <summary>
/// Removes a group of uncategorized transactions from the Quick-Categorize
/// wizard. Does not change category, balance, or any financial field — only
/// flips wizard-visibility flags on the rows the user owns.
/// </summary>
public class DismissUncategorizedGroupCommand : IRequest<DismissUncategorizedGroupResult>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    public DismissUncategorizedGroupMode Mode { get; set; } = DismissUncategorizedGroupMode.Snooze;

    /// <summary>
    /// Snooze window in days. Only used when <see cref="Mode"/> is
    /// <see cref="DismissUncategorizedGroupMode.Snooze"/>. Defaults to 30 days,
    /// matching the wizard's "Skip for now" button.
    /// </summary>
    public int SnoozeDays { get; set; } = 30;
}

public class DismissUncategorizedGroupResult
{
    public bool Success { get; set; }
    public int TransactionsUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DismissUncategorizedGroupCommandHandler
    : IRequestHandler<DismissUncategorizedGroupCommand, DismissUncategorizedGroupResult>
{
    private const int MaxIdsPerRequest = 500;

    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<DismissUncategorizedGroupCommandHandler> _logger;

    public DismissUncategorizedGroupCommandHandler(
        ITransactionRepository transactionRepository,
        ILogger<DismissUncategorizedGroupCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<DismissUncategorizedGroupResult> Handle(
        DismissUncategorizedGroupCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TransactionIds == null || request.TransactionIds.Count == 0)
        {
            return new DismissUncategorizedGroupResult
            {
                Success = false,
                Message = "No transaction IDs provided"
            };
        }

        if (request.TransactionIds.Count > MaxIdsPerRequest)
        {
            return new DismissUncategorizedGroupResult
            {
                Success = false,
                Message = $"Maximum {MaxIdsPerRequest} transactions can be dismissed per request"
            };
        }

        // GetTransactionsByIdsAsync already filters to transactions the user
        // owns (accessible accounts), so a crafted request with someone else's
        // ids returns an empty list rather than affecting their rows.
        var transactions = (await _transactionRepository.GetTransactionsByIdsAsync(
            request.TransactionIds,
            request.UserId,
            cancellationToken)).ToList();

        if (transactions.Count == 0)
        {
            return new DismissUncategorizedGroupResult
            {
                Success = false,
                Message = "Transactions not found or access denied"
            };
        }

        var now = DateTime.UtcNow;
        var snoozeUntil = now.AddDays(Math.Max(1, request.SnoozeDays));

        foreach (var transaction in transactions)
        {
            if (request.Mode == DismissUncategorizedGroupMode.Ignore)
            {
                transaction.IsHiddenFromQuickCategorize = true;
                transaction.QuickCategorizeSnoozedUntil = null;
            }
            else
            {
                transaction.QuickCategorizeSnoozedUntil = snoozeUntil;
            }

            transaction.UpdatedAt = now;
            transaction.UpdatedBy = request.UserId.ToString();
        }

        await _transactionRepository.SaveChangesAsync();

        _logger.LogInformation(
            "Dismissed {Count} uncategorized transactions for user {UserId} (mode={Mode})",
            transactions.Count, request.UserId, request.Mode);

        return new DismissUncategorizedGroupResult
        {
            Success = true,
            TransactionsUpdated = transactions.Count,
            Message = request.Mode == DismissUncategorizedGroupMode.Ignore
                ? "Transactions hidden from Quick Categorize"
                : "Transactions snoozed"
        };
    }
}
