namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the different types of categorization rules
/// </summary>
public enum RuleType
{
    /// <summary>
    /// Pattern must be contained anywhere in the description
    /// </summary>
    Contains = 1,

    /// <summary>
    /// Description must start with the pattern
    /// </summary>
    StartsWith = 2,

    /// <summary>
    /// Description must end with the pattern
    /// </summary>
    EndsWith = 3,

    /// <summary>
    /// Description must exactly equal the pattern
    /// </summary>
    Equals = 4,

    /// <summary>
    /// Pattern is a regular expression
    /// </summary>
    Regex = 5
}