namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for permanently deleting all user data for LGPD/GDPR compliance (right to be forgotten).
/// </summary>
public interface IUserDataDeletionService
{
    /// <summary>
    /// Permanently deletes all data associated with a user account.
    /// This operation is irreversible.
    /// </summary>
    /// <param name="userId">The user ID to delete data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of deleted data.</returns>
    Task<UserDeletionResultDto> DeleteAllUserDataAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a user data deletion operation.
/// </summary>
public class UserDeletionResultDto
{
    public Guid UserId { get; set; }
    public DateTime DeletedAt { get; set; }
    public int AccountsDeleted { get; set; }
    public int TransactionsDeleted { get; set; }
    public int CategoriesDeleted { get; set; }
    public int RulesDeleted { get; set; }
    public int TransfersDeleted { get; set; }
    public int ReconciliationsDeleted { get; set; }
    public int BankConnectionsDeleted { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
