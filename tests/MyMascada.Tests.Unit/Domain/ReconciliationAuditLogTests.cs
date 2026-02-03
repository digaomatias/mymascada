using FluentAssertions;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Text.Json;

namespace MyMascada.Tests.Unit.Domain;

public class ReconciliationAuditLogTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var auditLog = new ReconciliationAuditLog();

        // Assert
        auditLog.Id.Should().Be(0);
        auditLog.Action.Should().Be(ReconciliationAction.ReconciliationStarted);
        auditLog.UserId.Should().Be(Guid.Empty);
        auditLog.Details.Should().BeNull();
        auditLog.OldValues.Should().BeNull();
        auditLog.NewValues.Should().BeNull();
        auditLog.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SetProperties_ShouldUpdateCorrectly()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var userId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        // Act
        auditLog.ReconciliationId = 1;
        auditLog.Action = ReconciliationAction.TransactionMatched;
        auditLog.UserId = userId;
        auditLog.Timestamp = timestamp;

        // Assert
        auditLog.ReconciliationId.Should().Be(1);
        auditLog.Action.Should().Be(ReconciliationAction.TransactionMatched);
        auditLog.UserId.Should().Be(userId);
        auditLog.Timestamp.Should().Be(timestamp);
    }

    [Theory]
    [InlineData(ReconciliationAction.ReconciliationStarted)]
    [InlineData(ReconciliationAction.TransactionMatched)]
    [InlineData(ReconciliationAction.TransactionUnmatched)]
    [InlineData(ReconciliationAction.AdjustmentAdded)]
    [InlineData(ReconciliationAction.BankStatementImported)]
    [InlineData(ReconciliationAction.ReconciliationCompleted)]
    [InlineData(ReconciliationAction.ReconciliationCancelled)]
    [InlineData(ReconciliationAction.ManualTransactionAdded)]
    [InlineData(ReconciliationAction.TransactionDeleted)]
    public void Action_ShouldAcceptAllValidValues(ReconciliationAction action)
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();

        // Act
        auditLog.Action = action;

        // Assert
        auditLog.Action.Should().Be(action);
    }

    [Fact]
    public void SetDetails_WithObject_ShouldSerializeToJson()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var testDetails = new { 
            TransactionId = 123,
            MatchMethod = "ExactAmount",
            Confidence = 0.95
        };

        // Act
        auditLog.SetDetails(testDetails);

        // Assert
        auditLog.Details.Should().NotBeNull();
        var deserializedDetails = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Details!);
        deserializedDetails.Should().NotBeNull();
        deserializedDetails.Should().ContainKey("TransactionId");
        deserializedDetails.Should().ContainKey("MatchMethod");
        deserializedDetails.Should().ContainKey("Confidence");
    }

    [Fact]
    public void SetDetails_WithNull_ShouldSetToNull()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();

        // Act
        auditLog.SetDetails<object>(null);

        // Assert
        auditLog.Details.Should().BeNull();
    }

    [Fact]
    public void GetDetails_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var testDetails = new { 
            TransactionId = 123,
            Note = "Manual match applied"
        };
        auditLog.SetDetails(testDetails);

        // Act
        var result = auditLog.GetDetails<Dictionary<string, object>>();

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("TransactionId");
        result.Should().ContainKey("Note");
    }

    [Fact]
    public void GetDetails_WithNullData_ShouldReturnDefault()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();

        // Act
        var result = auditLog.GetDetails<object>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDetails_WithInvalidJson_ShouldReturnDefault()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        auditLog.Details = "invalid json {";

        // Act
        var result = auditLog.GetDetails<object>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SetOldValues_WithObject_ShouldSerializeToJson()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var oldValues = new { 
            Status = "InProgress",
            StatementEndBalance = 1000.00m
        };

        // Act
        auditLog.SetOldValues(oldValues);

        // Assert
        auditLog.OldValues.Should().NotBeNull();
        var deserializedValues = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.OldValues!);
        deserializedValues.Should().NotBeNull();
        deserializedValues.Should().ContainKey("Status");
        deserializedValues.Should().ContainKey("StatementEndBalance");
    }

    [Fact]
    public void SetNewValues_WithObject_ShouldSerializeToJson()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var newValues = new { 
            Status = "Completed",
            StatementEndBalance = 1050.00m
        };

        // Act
        auditLog.SetNewValues(newValues);

        // Assert
        auditLog.NewValues.Should().NotBeNull();
        var deserializedValues = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.NewValues!);
        deserializedValues.Should().NotBeNull();
        deserializedValues.Should().ContainKey("Status");
        deserializedValues.Should().ContainKey("StatementEndBalance");
    }

    [Fact]
    public void GetOldValues_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var oldValues = new { Status = "InProgress" };
        auditLog.SetOldValues(oldValues);

        // Act
        var result = auditLog.GetOldValues<Dictionary<string, object>>();

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("Status");
    }

    [Fact]
    public void GetNewValues_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var newValues = new { Status = "Completed" };
        auditLog.SetNewValues(newValues);

        // Act
        var result = auditLog.GetNewValues<Dictionary<string, object>>();

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("Status");
    }

    [Fact]
    public void Timestamp_ShouldBeSettable()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var timestamp = DateTime.UtcNow;

        // Act
        auditLog.Timestamp = timestamp;

        // Assert
        auditLog.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void UserId_ShouldBeSettable()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var userId = Guid.NewGuid();

        // Act
        auditLog.UserId = userId;

        // Assert
        auditLog.UserId.Should().Be(userId);
    }

    [Fact]
    public void AuditFields_ShouldBeSettable()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var now = DateTime.UtcNow;

        // Act
        auditLog.CreatedAt = now;
        auditLog.UpdatedAt = now;
        auditLog.CreatedBy = "test-user";
        auditLog.UpdatedBy = "test-user";

        // Assert
        auditLog.CreatedAt.Should().Be(now);
        auditLog.UpdatedAt.Should().Be(now);
        auditLog.CreatedBy.Should().Be("test-user");
        auditLog.UpdatedBy.Should().Be("test-user");
    }

    [Fact]
    public void SoftDelete_ShouldSetDeletedFields()
    {
        // Arrange
        var auditLog = new ReconciliationAuditLog();
        var deletedAt = DateTime.UtcNow;

        // Act
        auditLog.IsDeleted = true;
        auditLog.DeletedAt = deletedAt;

        // Assert
        auditLog.IsDeleted.Should().BeTrue();
        auditLog.DeletedAt.Should().Be(deletedAt);
    }
}