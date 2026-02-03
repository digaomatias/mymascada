namespace MyMascada.Domain.Common;

/// <summary>
/// Interface for entities that support audit tracking.
/// Provides automatic tracking of creation and modification timestamps.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}

/// <summary>
/// Interface for entities that support soft deletion.
/// Allows marking entities as deleted without physical removal from database.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}