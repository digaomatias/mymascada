namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the different comparison operators that can be used in rule conditions
/// </summary>
public enum RuleConditionOperator
{
    /// <summary>
    /// Field value must equal the specified value
    /// </summary>
    Equals = 1,

    /// <summary>
    /// Field value must not equal the specified value
    /// </summary>
    NotEquals = 2,

    /// <summary>
    /// Field value must contain the specified value
    /// </summary>
    Contains = 3,

    /// <summary>
    /// Field value must not contain the specified value
    /// </summary>
    NotContains = 4,

    /// <summary>
    /// Field value must start with the specified value
    /// </summary>
    StartsWith = 5,

    /// <summary>
    /// Field value must end with the specified value
    /// </summary>
    EndsWith = 6,

    /// <summary>
    /// Field value must be greater than the specified value (numeric fields only)
    /// </summary>
    GreaterThan = 7,

    /// <summary>
    /// Field value must be less than the specified value (numeric fields only)
    /// </summary>
    LessThan = 8,

    /// <summary>
    /// Field value must be greater than or equal to the specified value (numeric fields only)
    /// </summary>
    GreaterThanOrEqual = 9,

    /// <summary>
    /// Field value must be less than or equal to the specified value (numeric fields only)
    /// </summary>
    LessThanOrEqual = 10,

    /// <summary>
    /// Field value must match the specified regular expression
    /// </summary>
    Regex = 11
}