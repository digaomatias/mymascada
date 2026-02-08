using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Command to categorize a single transaction using the full categorization pipeline
/// </summary>
public class CategorizeTransactionCommand : IRequest
{
    public int TransactionId { get; set; }
    public Guid UserId { get; set; }
}

/// <summary>
/// Handler for categorizing individual transactions through the pipeline
/// </summary>
public class CategorizeTransactionCommandHandler : IRequestHandler<CategorizeTransactionCommand>
{
    private readonly ICategorizationPipeline _categorizationPipeline;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly ILogger<CategorizeTransactionCommandHandler> _logger;

    public CategorizeTransactionCommandHandler(
        ICategorizationPipeline categorizationPipeline,
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService,
        ILogger<CategorizeTransactionCommandHandler> logger)
    {
        _categorizationPipeline = categorizationPipeline;
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
        _logger = logger;
    }

    public async Task Handle(CategorizeTransactionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting categorization for transaction {TransactionId}", request.TransactionId);

        try
        {
            // Fetch the transaction
            var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, request.UserId);
            if (transaction == null)
            {
                _logger.LogWarning("Transaction {TransactionId} not found for categorization", request.TransactionId);
                return;
            }

            // Verify the user has modify permission on the transaction's account (owner or Manager role)
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
            {
                throw new UnauthorizedAccessException("You do not have permission to categorize transactions on this account.");
            }

            // Process through the full categorization pipeline: Rules → ML → LLM
            var result = await _categorizationPipeline.ProcessAsync(new[] { transaction }, cancellationToken);

            _logger.LogDebug("Categorization completed for transaction {TransactionId}. " +
                "Auto-applied: {AutoApplied}, Candidates: {Candidates}, Remaining: {Remaining}",
                request.TransactionId,
                result.AutoAppliedTransactions.Count,
                result.Candidates.Count,
                result.RemainingTransactions.Count);

            if (result.Errors.Any())
            {
                _logger.LogWarning("Categorization for transaction {TransactionId} completed with errors: {Errors}",
                    request.TransactionId, string.Join("; ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to categorize transaction {TransactionId}", request.TransactionId);
            throw; // Re-throw to trigger Hangfire retry
        }
    }
}