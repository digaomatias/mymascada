using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.Text.Json;

namespace MyMascada.Domain.Entities;

public class ReconciliationAuditLog : BaseEntity
{
    public int ReconciliationId { get; set; }
    public ReconciliationAction Action { get; set; }
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; } // JSON string for action-specific data
    public string? OldValues { get; set; } // JSON string for previous values
    public string? NewValues { get; set; } // JSON string for new values

    // Navigation properties
    public virtual Reconciliation Reconciliation { get; set; } = null!;

    // Helper methods for JSON data
    public void SetDetails<T>(T data) where T : class
    {
        Details = data != null ? JsonSerializer.Serialize(data) : null;
    }

    public T? GetDetails<T>() where T : class
    {
        if (string.IsNullOrEmpty(Details))
            return null;
        
        try
        {
            return JsonSerializer.Deserialize<T>(Details);
        }
        catch
        {
            return null;
        }
    }

    public void SetOldValues<T>(T data) where T : class
    {
        OldValues = data != null ? JsonSerializer.Serialize(data) : null;
    }

    public T? GetOldValues<T>() where T : class
    {
        if (string.IsNullOrEmpty(OldValues))
            return null;
        
        try
        {
            return JsonSerializer.Deserialize<T>(OldValues);
        }
        catch
        {
            return null;
        }
    }

    public void SetNewValues<T>(T data) where T : class
    {
        NewValues = data != null ? JsonSerializer.Serialize(data) : null;
    }

    public T? GetNewValues<T>() where T : class
    {
        if (string.IsNullOrEmpty(NewValues))
            return null;
        
        try
        {
            return JsonSerializer.Deserialize<T>(NewValues);
        }
        catch
        {
            return null;
        }
    }
}