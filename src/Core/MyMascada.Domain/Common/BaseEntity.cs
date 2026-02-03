using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Common;

/// <summary>
/// Base entity class that provides common properties for all domain entities.
/// Follows DDD principles with strongly-typed IDs and audit fields.
/// </summary>
public abstract class BaseEntity<TId> : IAuditableEntity, ISoftDeletable where TId : struct
{
    private DateTime _createdAt;
    private DateTime _updatedAt;

    /// <summary>
    /// Unique identifier for the entity
    /// </summary>
    [Key]
    public TId Id { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was created
    /// </summary>
    public DateTime CreatedAt 
    { 
        get => _createdAt == default ? DateTimeProvider.UtcNow : _createdAt;
        set => _createdAt = DateTimeProvider.ToUtc(value);
    }

    /// <summary>
    /// UTC timestamp when the entity was last updated
    /// </summary>
    public DateTime UpdatedAt 
    { 
        get => _updatedAt == default ? DateTimeProvider.UtcNow : _updatedAt;
        set => _updatedAt = DateTimeProvider.ToUtc(value);
    }

    /// <summary>
    /// Soft delete flag - entity is marked as deleted but not physically removed
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was marked as deleted
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// User ID who created this entity
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User ID who last updated this entity
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Constructor to initialize timestamps
    /// </summary>
    protected BaseEntity()
    {
        var now = DateTimeProvider.UtcNow;
        _createdAt = now;
        _updatedAt = now;
    }
}

/// <summary>
/// Base entity with integer ID for simpler entities
/// </summary>
public abstract class BaseEntity : BaseEntity<int>
{
}