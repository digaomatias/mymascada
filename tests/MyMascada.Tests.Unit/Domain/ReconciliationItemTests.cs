using FluentAssertions;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Text.Json;

namespace MyMascada.Tests.Unit.Domain;

public class ReconciliationItemTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var item = new ReconciliationItem();

        // Assert
        item.Id.Should().Be(0);
        item.ItemType.Should().Be(ReconciliationItemType.Matched);
        item.MatchConfidence.Should().BeNull();
        item.MatchMethod.Should().BeNull();
        item.BankReferenceData.Should().BeNull();
        item.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SetProperties_ShouldUpdateCorrectly()
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        item.ReconciliationId = 1;
        item.TransactionId = 100;
        item.ItemType = ReconciliationItemType.UnmatchedApp;
        item.MatchConfidence = 0.95m;
        item.MatchMethod = MatchMethod.Exact;

        // Assert
        item.ReconciliationId.Should().Be(1);
        item.TransactionId.Should().Be(100);
        item.ItemType.Should().Be(ReconciliationItemType.UnmatchedApp);
        item.MatchConfidence.Should().Be(0.95m);
        item.MatchMethod.Should().Be(MatchMethod.Exact);
    }

    [Theory]
    [InlineData(ReconciliationItemType.Matched)]
    [InlineData(ReconciliationItemType.UnmatchedApp)]
    [InlineData(ReconciliationItemType.UnmatchedBank)]
    [InlineData(ReconciliationItemType.Adjustment)]
    public void ItemType_ShouldAcceptAllValidValues(ReconciliationItemType itemType)
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        item.ItemType = itemType;

        // Assert
        item.ItemType.Should().Be(itemType);
    }

    [Theory]
    [InlineData(MatchMethod.Exact)]
    [InlineData(MatchMethod.Fuzzy)]
    [InlineData(MatchMethod.Manual)]
    [InlineData(MatchMethod.AmountOnly)]
    public void MatchMethod_ShouldAcceptAllValidValues(MatchMethod matchMethod)
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        item.MatchMethod = matchMethod;

        // Assert
        item.MatchMethod.Should().Be(matchMethod);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.95)]
    [InlineData(1.0)]
    public void MatchConfidence_ShouldAcceptValidValues(decimal confidence)
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        item.MatchConfidence = confidence;

        // Assert
        item.MatchConfidence.Should().Be(confidence);
    }

    [Fact]
    public void SetBankReferenceData_WithObject_ShouldSerializeToJson()
    {
        // Arrange
        var item = new ReconciliationItem();
        var testData = new { 
            BankTransactionId = "ABC123",
            BankDescription = "GROCERY STORE #123",
            BankCategory = "Food & Dining"
        };

        // Act
        item.SetBankReferenceData(testData);

        // Assert
        item.BankReferenceData.Should().NotBeNull();
        var deserializedData = JsonSerializer.Deserialize<Dictionary<string, object>>(item.BankReferenceData!);
        deserializedData.Should().NotBeNull();
        deserializedData.Should().ContainKey("BankTransactionId");
        deserializedData.Should().ContainKey("BankDescription");
        deserializedData.Should().ContainKey("BankCategory");
    }

    [Fact]
    public void SetBankReferenceData_WithNull_ShouldSetToNull()
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        item.SetBankReferenceData<object>(null);

        // Assert
        item.BankReferenceData.Should().BeNull();
    }

    [Fact]
    public void GetBankReferenceData_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var item = new ReconciliationItem();
        var testData = new { 
            BankTransactionId = "ABC123",
            BankDescription = "GROCERY STORE #123"
        };
        item.SetBankReferenceData(testData);

        // Act - Use a concrete type instead of dynamic for System.Text.Json
        var result = item.GetBankReferenceData<Dictionary<string, object>>();

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("BankTransactionId");
        result.Should().ContainKey("BankDescription");
    }

    [Fact]
    public void GetBankReferenceData_WithNullData_ShouldReturnDefault()
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        var result = item.GetBankReferenceData<object>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetBankReferenceData_WithInvalidJson_ShouldReturnDefault()
    {
        // Arrange
        var item = new ReconciliationItem();
        item.BankReferenceData = "invalid json {";

        // Act
        var result = item.GetBankReferenceData<object>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TransactionId_WithNull_ShouldBeAllowed()
    {
        // Arrange
        var item = new ReconciliationItem();

        // Act
        item.TransactionId = null;

        // Assert
        item.TransactionId.Should().BeNull();
    }

    [Fact]
    public void AuditFields_ShouldBeSettable()
    {
        // Arrange
        var item = new ReconciliationItem();
        var now = DateTime.UtcNow;

        // Act
        item.CreatedAt = now;
        item.UpdatedAt = now;
        item.CreatedBy = "test-user";
        item.UpdatedBy = "test-user";

        // Assert
        item.CreatedAt.Should().Be(now);
        item.UpdatedAt.Should().Be(now);
        item.CreatedBy.Should().Be("test-user");
        item.UpdatedBy.Should().Be("test-user");
    }

    [Fact]
    public void SoftDelete_ShouldSetDeletedFields()
    {
        // Arrange
        var item = new ReconciliationItem();
        var deletedAt = DateTime.UtcNow;

        // Act
        item.IsDeleted = true;
        item.DeletedAt = deletedAt;

        // Assert
        item.IsDeleted.Should().BeTrue();
        item.DeletedAt.Should().Be(deletedAt);
    }
}