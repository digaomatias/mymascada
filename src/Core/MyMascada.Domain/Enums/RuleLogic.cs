namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents how multiple conditions within a rule should be combined
/// </summary>
public enum RuleLogic
{
    /// <summary>
    /// All conditions must be true for the rule to match (AND logic)
    /// </summary>
    All = 1,

    /// <summary>
    /// Any condition can be true for the rule to match (OR logic)
    /// </summary>
    Any = 2
}