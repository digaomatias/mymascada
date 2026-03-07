namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a budget period
/// </summary>
public enum BudgetStatus
{
    /// <summary>
    /// Budget is currently active and tracking spending
    /// </summary>
    Active = 0,

    /// <summary>
    /// Budget period ended (new period was created for recurring, or non-recurring expired)
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Budget was soft-deleted/cancelled by the user
    /// </summary>
    Cancelled = 2
}
