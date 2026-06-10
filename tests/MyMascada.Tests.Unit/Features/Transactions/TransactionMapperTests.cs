using MyMascada.Application.Features.Transactions.Mappings;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Features.Transactions;

public class TransactionMapperTests
{
    private static Transaction CreateTransaction(List<TransactionSplit>? splits = null)
    {
        return new Transaction
        {
            Id = 1,
            Amount = -100m,
            TransactionDate = DateTime.UtcNow,
            Description = "Test transaction",
            AccountId = 10,
            Type = TransactionType.Expense,
            Splits = splits ?? new List<TransactionSplit>(),
            Account = new Account { Id = 10, Name = "Checking", UserId = Guid.NewGuid() }
        };
    }

    [Fact]
    public void ToDetailDto_ShouldFilterSoftDeletedSplits()
    {
        var transaction = CreateTransaction(new List<TransactionSplit>
        {
            new() { Id = 1, TransactionId = 1, CategoryId = 1, Amount = -60m },
            new() { Id = 2, TransactionId = 1, CategoryId = 2, Amount = -40m, IsDeleted = true }
        });

        var dto = TransactionMapper.ToDetailDto(transaction);

        dto.Splits.Should().NotBeNull();
        dto.Splits.Should().HaveCount(1);
        dto.Splits![0].CategoryId.Should().Be(1);
    }

    [Fact]
    public void ToDetailDto_WhenAllSplitsSoftDeleted_ShouldReturnNullSplits()
    {
        var transaction = CreateTransaction(new List<TransactionSplit>
        {
            new() { Id = 1, TransactionId = 1, CategoryId = 1, Amount = -100m, IsDeleted = true }
        });

        var dto = TransactionMapper.ToDetailDto(transaction);

        dto.Splits.Should().BeNull();
    }

    [Fact]
    public void ToDto_ShouldFilterSoftDeletedSplits()
    {
        var transaction = CreateTransaction(new List<TransactionSplit>
        {
            new() { Id = 1, TransactionId = 1, CategoryId = 1, Amount = -100m },
            new() { Id = 2, TransactionId = 1, CategoryId = 2, Amount = -40m, IsDeleted = true }
        });

        var dto = TransactionMapper.ToDto(transaction);

        dto.Splits.Should().NotBeNull();
        dto.Splits.Should().HaveCount(1);
        dto.Splits![0].CategoryId.Should().Be(1);
    }
}
