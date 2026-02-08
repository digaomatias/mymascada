namespace MyMascada.Domain.Enums;

/// <summary>
/// Defines the level of access granted when an account is shared with another user.
/// </summary>
public enum AccountShareRole
{
    /// <summary>
    /// Read-only access to account transactions and balances
    /// </summary>
    Viewer = 1,

    /// <summary>
    /// Read and write access: can create/edit transactions and categorize
    /// </summary>
    Manager = 2
}
