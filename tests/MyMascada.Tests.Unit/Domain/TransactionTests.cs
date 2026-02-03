using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Domain;

public class TransactionTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var transaction = new Transaction();

        // Assert
        transaction.Id.Should().Be(0);
        transaction.Amount.Should().Be(0);
        transaction.Status.Should().Be(TransactionStatus.Cleared);
        transaction.Source.Should().Be(TransactionSource.Manual);
        transaction.IsReviewed.Should().BeFalse();
        transaction.IsExcluded.Should().BeFalse();
        transaction.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SetProperties_ShouldUpdateCorrectly()
    {
        // Arrange
        var transaction = new Transaction();
        var transactionDate = new DateTime(2025, 6, 18);

        // Act
        transaction.Amount = -87.43m;
        transaction.Description = "Grocery Store";
        transaction.TransactionDate = transactionDate;
        transaction.Status = TransactionStatus.Cleared;
        transaction.AccountId = 1;

        // Assert
        transaction.Amount.Should().Be(-87.43m);
        transaction.Description.Should().Be("Grocery Store");
        transaction.TransactionDate.Should().Be(transactionDate);
        transaction.Status.Should().Be(TransactionStatus.Cleared);
        transaction.AccountId.Should().Be(1);
    }

    [Theory]
    [InlineData(TransactionStatus.Pending)]
    [InlineData(TransactionStatus.Cleared)]
    [InlineData(TransactionStatus.Reconciled)]
    [InlineData(TransactionStatus.Cancelled)]
    public void Status_ShouldAcceptAllValidValues(TransactionStatus status)
    {
        // Arrange
        var transaction = new Transaction();

        // Act
        transaction.Status = status;

        // Assert
        transaction.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(TransactionSource.Manual)]
    [InlineData(TransactionSource.CsvImport)]
    [InlineData(TransactionSource.BankApi)]
    [InlineData(TransactionSource.OfxImport)]
    public void Source_ShouldAcceptAllValidValues(TransactionSource source)
    {
        // Arrange
        var transaction = new Transaction();

        // Act
        transaction.Source = source;

        // Assert
        transaction.Source.Should().Be(source);
    }

    [Fact]
    public void ExternalId_WhenSet_ShouldRetainValue()
    {
        // Arrange
        var transaction = new Transaction();
        const string externalId = "EXT123456";

        // Act
        transaction.ExternalId = externalId;

        // Assert
        transaction.ExternalId.Should().Be(externalId);
    }

    [Fact]
    public void AuditFields_ShouldBeSettable()
    {
        // Arrange
        var transaction = new Transaction();
        var now = DateTime.UtcNow;

        // Act
        transaction.CreatedAt = now;
        transaction.UpdatedAt = now;

        // Assert
        transaction.CreatedAt.Should().Be(now);
        transaction.UpdatedAt.Should().Be(now);
    }
}