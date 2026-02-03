using FluentAssertions;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Domain;

public class ReconciliationTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var reconciliation = new Reconciliation();

        // Assert
        reconciliation.Id.Should().Be(0);
        reconciliation.Status.Should().Be(ReconciliationStatus.InProgress);
        reconciliation.StatementEndBalance.Should().Be(0);
        reconciliation.CalculatedBalance.Should().BeNull();
        reconciliation.IsDeleted.Should().BeFalse();
        reconciliation.ReconciliationItems.Should().NotBeNull();
        reconciliation.AuditLogs.Should().NotBeNull();
    }

    [Fact]
    public void SetProperties_ShouldUpdateCorrectly()
    {
        // Arrange
        var reconciliation = new Reconciliation();
        var reconciliationDate = new DateTime(2025, 7, 8);
        var statementEndDate = new DateTime(2025, 7, 7);
        var userId = Guid.NewGuid();

        // Act
        reconciliation.AccountId = 1;
        reconciliation.ReconciliationDate = reconciliationDate;
        reconciliation.StatementEndDate = statementEndDate;
        reconciliation.StatementEndBalance = 1500.75m;
        reconciliation.CalculatedBalance = 1502.25m;
        reconciliation.Status = ReconciliationStatus.Completed;
        reconciliation.CreatedByUserId = userId;
        reconciliation.Notes = "Test reconciliation";

        // Assert
        reconciliation.AccountId.Should().Be(1);
        reconciliation.ReconciliationDate.Should().Be(reconciliationDate);
        reconciliation.StatementEndDate.Should().Be(statementEndDate);
        reconciliation.StatementEndBalance.Should().Be(1500.75m);
        reconciliation.CalculatedBalance.Should().Be(1502.25m);
        reconciliation.Status.Should().Be(ReconciliationStatus.Completed);
        reconciliation.CreatedByUserId.Should().Be(userId);
        reconciliation.Notes.Should().Be("Test reconciliation");
    }

    [Theory]
    [InlineData(ReconciliationStatus.InProgress)]
    [InlineData(ReconciliationStatus.Completed)]
    [InlineData(ReconciliationStatus.Cancelled)]
    public void Status_ShouldAcceptAllValidValues(ReconciliationStatus status)
    {
        // Arrange
        var reconciliation = new Reconciliation();

        // Act
        reconciliation.Status = status;

        // Assert
        reconciliation.Status.Should().Be(status);
    }

    [Fact]
    public void BalanceDifference_ExactMatch_ShouldBeZeroAndBalanced()
    {
        // Arrange
        var reconciliation = new Reconciliation
        {
            StatementEndBalance = 1000m,
            CalculatedBalance = 1000m
        };

        // Act & Assert
        reconciliation.BalanceDifference.Should().Be(0m);
        reconciliation.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void BalanceDifference_WithinTolerance_ShouldBeBalanced()
    {
        // Arrange
        var reconciliation1 = new Reconciliation
        {
            StatementEndBalance = 1000m,
            CalculatedBalance = 1000.01m
        };
        
        var reconciliation2 = new Reconciliation
        {
            StatementEndBalance = 1000m,
            CalculatedBalance = 999.99m
        };

        // Act & Assert
        reconciliation1.BalanceDifference.Should().Be(-0.01m);
        reconciliation1.IsBalanced.Should().BeTrue();
        
        reconciliation2.BalanceDifference.Should().Be(0.01m);
        reconciliation2.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void BalanceDifference_OutsideTolerance_ShouldNotBeBalanced()
    {
        // Arrange
        var reconciliation1 = new Reconciliation
        {
            StatementEndBalance = 1000m,
            CalculatedBalance = 1000.02m
        };
        
        var reconciliation2 = new Reconciliation
        {
            StatementEndBalance = 1000m,
            CalculatedBalance = 999.98m
        };

        // Act & Assert
        reconciliation1.BalanceDifference.Should().Be(-0.02m);
        reconciliation1.IsBalanced.Should().BeFalse();
        
        reconciliation2.BalanceDifference.Should().Be(0.02m);
        reconciliation2.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void BalanceDifference_NullCalculatedBalance_ShouldNotBeBalanced()
    {
        // Arrange
        var reconciliation = new Reconciliation
        {
            StatementEndBalance = 1000m,
            CalculatedBalance = null
        };

        // Act & Assert
        reconciliation.BalanceDifference.Should().Be(1000m);
        reconciliation.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void TotalItemsCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var reconciliation = new Reconciliation();
        
        // Add some reconciliation items
        reconciliation.ReconciliationItems.Add(new ReconciliationItem());
        reconciliation.ReconciliationItems.Add(new ReconciliationItem());
        reconciliation.ReconciliationItems.Add(new ReconciliationItem());

        // Act & Assert
        reconciliation.TotalItemsCount.Should().Be(3);
    }

    [Fact]
    public void MatchedItemsCount_ShouldCountOnlyMatchedItems()
    {
        // Arrange
        var reconciliation = new Reconciliation();
        
        // Add mixed reconciliation items
        reconciliation.ReconciliationItems.Add(new ReconciliationItem { ItemType = ReconciliationItemType.Matched });
        reconciliation.ReconciliationItems.Add(new ReconciliationItem { ItemType = ReconciliationItemType.UnmatchedApp });
        reconciliation.ReconciliationItems.Add(new ReconciliationItem { ItemType = ReconciliationItemType.Matched });
        reconciliation.ReconciliationItems.Add(new ReconciliationItem { ItemType = ReconciliationItemType.UnmatchedBank });

        // Act & Assert
        reconciliation.MatchedItemsCount.Should().Be(2);
    }

    [Theory]
    [InlineData(0, 0, 0)]      // No items
    [InlineData(4, 2, 50)]     // Half matched
    [InlineData(3, 3, 100)]    // All matched
    [InlineData(5, 1, 20)]     // One fifth matched
    public void MatchedPercentage_ShouldCalculateCorrectly(int totalItems, int matchedItems, decimal expectedPercentage)
    {
        // Arrange
        var reconciliation = new Reconciliation();
        
        // Add total items
        for (int i = 0; i < totalItems; i++)
        {
            var itemType = i < matchedItems ? ReconciliationItemType.Matched : ReconciliationItemType.UnmatchedApp;
            reconciliation.ReconciliationItems.Add(new ReconciliationItem { ItemType = itemType });
        }

        // Act & Assert
        reconciliation.MatchedPercentage.Should().Be(expectedPercentage);
    }

    [Fact]
    public void CompletedAt_ShouldBeSettable()
    {
        // Arrange
        var reconciliation = new Reconciliation();
        var completedAt = DateTime.UtcNow;

        // Act
        reconciliation.CompletedAt = completedAt;

        // Assert
        reconciliation.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void AuditFields_ShouldBeSettable()
    {
        // Arrange
        var reconciliation = new Reconciliation();
        var now = DateTime.UtcNow;

        // Act
        reconciliation.CreatedAt = now;
        reconciliation.UpdatedAt = now;
        reconciliation.CreatedBy = "test-user";
        reconciliation.UpdatedBy = "test-user";

        // Assert
        reconciliation.CreatedAt.Should().Be(now);
        reconciliation.UpdatedAt.Should().Be(now);
        reconciliation.CreatedBy.Should().Be("test-user");
        reconciliation.UpdatedBy.Should().Be("test-user");
    }
}