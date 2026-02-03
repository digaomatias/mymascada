using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Service for applying categorization rules to filtered transactions
/// Designed for manual user-initiated rule application in the categorization UI
/// </summary>
public interface IRuleAutoCategorizationService
{
    /// <summary>
    /// Previews what would happen if rules were applied to filtered transactions
    /// Shows count of transactions that would be categorized without actually applying changes
    /// </summary>
    /// <param name="filterCriteria">Current UI filter criteria</param> 
    /// <param name="userId">User ID for security context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preview result showing potential rule matches</returns>
    Task<RuleAutoCategorizationResult> PreviewRuleApplicationAsync(
        TransactionFilterCriteria filterCriteria, 
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies categorization rules to all filtered transactions
    /// Only processes transactions that don't already have rule candidates
    /// </summary>
    /// <param name="filterCriteria">Current UI filter criteria</param>
    /// <param name="userId">User ID for security context</param> 
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with applied rule matches and created candidates</returns>
    Task<RuleAutoCategorizationResult> ApplyRulesToFilteredTransactionsAsync(
        TransactionFilterCriteria filterCriteria,
        Guid userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies selected rule matches to transactions
    /// Allows users to choose which rule suggestions to apply
    /// </summary>
    /// <param name="selectedMatches">Rule matches selected by user for application</param>
    /// <param name="userId">User ID for security context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with applied categorizations</returns>
    Task<RuleAutoCategorizationResult> ApplySelectedRuleMatchesAsync(
        List<RuleMatchDetail> selectedMatches,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter criteria matching the frontend categorization page filters
/// </summary>
public class TransactionFilterCriteria
{
    /// <summary>Filter by account IDs (empty = all accounts)</summary>
    public List<int> AccountIds { get; set; } = new();
    
    /// <summary>Filter by date range</summary>
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    /// <summary>Filter by amount range</summary>
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    
    /// <summary>Filter by transaction type (Income/Expense)</summary>
    public string? TransactionType { get; set; } // "Income", "Expense", or null for all
    
    /// <summary>Search text filter for description</summary>
    public string? SearchText { get; set; }
    
    /// <summary>Only show unreviewed transactions (default behavior)</summary>
    public bool OnlyUnreviewed { get; set; } = true;
    
    /// <summary>Exclude transfer transactions (default behavior)</summary>
    public bool ExcludeTransfers { get; set; } = true;
}

/// <summary>
/// Result of rule auto-categorization operation (preview or actual application)
/// </summary>
public class RuleAutoCategorizationResult
{
    /// <summary>Total transactions examined</summary>
    public int TotalTransactionsExamined { get; set; }
    
    /// <summary>Transactions that were skipped (already have rule candidates)</summary>
    public int TransactionsSkipped { get; set; }
    
    /// <summary>Transactions that matched rules</summary>
    public int TransactionsMatched { get; set; }
    
    /// <summary>Transactions that didn't match any rules</summary>
    public int TransactionsUnmatched { get; set; }
    
    /// <summary>Details of rule matches for user review</summary>
    public List<RuleMatchDetail> RuleMatches { get; set; } = new();
    
    /// <summary>Summary message for user display</summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>Whether this was a preview (true) or actual application (false)</summary>
    public bool IsPreview { get; set; }
    
    /// <summary>Any errors that occurred during processing</summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>Processing time for performance monitoring</summary>
    public TimeSpan ProcessingTime { get; set; }
    
    /// <summary>IDs of transactions that were successfully processed</summary>
    public List<int> ProcessedTransactionIds { get; set; } = new();
    
    /// <summary>Success indicator</summary>
    public bool IsSuccess => !Errors.Any();
}

/// <summary>
/// Details of a specific rule match for user review
/// </summary>
public class RuleMatchDetail
{
    /// <summary>Transaction that was matched</summary>
    public int TransactionId { get; set; }
    public string TransactionDescription { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
    
    /// <summary>Rule that matched</summary>
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RulePattern { get; set; } = string.Empty;
    
    /// <summary>Category that would be applied</summary>
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    
    /// <summary>Confidence score (rules typically have high confidence)</summary>
    public decimal ConfidenceScore { get; set; } = 0.95m;
    
    /// <summary>Would this change the transaction's current category?</summary>
    public bool WouldChangeCategory { get; set; }
    
    /// <summary>Current category name (if any)</summary>
    public string? CurrentCategoryName { get; set; }
    
    /// <summary>Whether this is an existing candidate or newly generated</summary>
    public bool IsExistingCandidate { get; set; }
    
    /// <summary>Candidate ID if this is an existing candidate (for selective application)</summary>
    public int? CandidateId { get; set; }
    
    /// <summary>Whether this match is selected for application (default based on confidence)</summary>
    public bool IsSelected { get; set; } = true;
    
    /// <summary>High confidence matches that can be auto-applied</summary>
    public bool CanAutoApply { get; set; }
}