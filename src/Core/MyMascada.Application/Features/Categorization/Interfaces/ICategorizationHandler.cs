using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Interfaces;

/// <summary>
/// Chain of Responsibility handler for transaction categorization
/// Implements the pipeline: Rules → ML → LLM
/// </summary>
public interface ICategorizationHandler
{
    /// <summary>
    /// Processes a batch of transactions for categorization
    /// </summary>
    Task<CategorizationResult> HandleAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the next handler in the chain
    /// </summary>
    ICategorizationHandler SetNext(ICategorizationHandler handler);
    
    /// <summary>
    /// Gets the handler type for logging and metrics
    /// </summary>
    string HandlerType { get; }
}