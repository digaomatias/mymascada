using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Tracks monthly AI categorization usage per user for quota enforcement.
/// One row per user per month.
/// </summary>
public class AiCategorizationUsage : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Calendar year (e.g. 2026).
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Calendar month (1-12).
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Number of transactions categorized via LLM this month.
    /// </summary>
    public int LlmCategorizationCount { get; set; }

    /// <summary>
    /// Number of AI rule suggestion generations this month.
    /// </summary>
    public int RuleSuggestionCount { get; set; }
}
