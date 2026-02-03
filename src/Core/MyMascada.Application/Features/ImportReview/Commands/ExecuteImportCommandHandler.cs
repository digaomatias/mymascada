using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Events;
using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Features.ImportReview.Commands;

public class ExecuteImportCommandHandler : IRequestHandler<ExecuteImportCommand, ImportExecutionResult>
{
    private readonly IImportAnalysisService _importAnalysisService;
    private readonly IMediator _mediator;
    private readonly ILogger<ExecuteImportCommandHandler> _logger;

    public ExecuteImportCommandHandler(
        IImportAnalysisService importAnalysisService,
        IMediator mediator,
        ILogger<ExecuteImportCommandHandler> logger)
    {
        _importAnalysisService = importAnalysisService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ImportExecutionResult> Handle(ExecuteImportCommand request, CancellationToken cancellationToken)
    {
        var executionRequest = new ImportExecutionRequest
        {
            AnalysisId = request.AnalysisId,
            AccountId = request.AccountId,
            UserId = request.UserId,
            Decisions = request.Decisions,
            SkipValidation = request.SkipValidation
        };

        var result = await _importAnalysisService.ExecuteImportAsync(executionRequest);

        // Publish event for asynchronous categorization processing if import was successful
        if (result.IsSuccess && result.ImportedTransactions?.Any() == true)
        {
            try
            {
                var transactionIds = result.ImportedTransactions
                    .Where(t => t.IsNew && !t.IsSkipped)
                    .Select(t => t.Id)
                    .ToList();
                
                if (transactionIds.Any())
                {
                    var transactionsCreatedEvent = new TransactionsCreatedEvent(
                        transactionIds, 
                        request.UserId);
                    
                    await _mediator.Publish(transactionsCreatedEvent, cancellationToken);
                    
                    _logger.LogInformation("Published TransactionsCreatedEvent for {TransactionCount} transactions from import review for async categorization", 
                        transactionIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish TransactionsCreatedEvent for user {UserId} from import review. Categorization will need to be done manually.", 
                    request.UserId);
                // Don't fail the import if event publishing fails
            }
        }

        return result;
    }
}