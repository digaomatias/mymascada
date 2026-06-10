using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Queries;

public class GetTransactionQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly GetTransactionQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetTransactionQueryHandlerTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _handler = new GetTransactionQueryHandler(_transactionRepository);
    }

    [Fact]
    public async Task Handle_WhenTransactionNotFound_ShouldReturnNull()
    {
        _transactionRepository.GetByIdWithSplitsAsync(99, _userId).Returns((Transaction?)null);

        var result = await _handler.Handle(
            new GetTransactionQuery { UserId = _userId, Id = 99 }, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTransactionHasSplits_ShouldIncludeActiveSplitsInDto()
    {
        var transaction = new Transaction
        {
            Id = 1,
            Amount = -100m,
            Description = "Supermarket",
            AccountId = 10,
            Account = new Account { Id = 10, Name = "Checking", UserId = _userId },
            Splits = new List<TransactionSplit>
            {
                new()
                {
                    Id = 1,
                    TransactionId = 1,
                    CategoryId = 5,
                    Amount = -60m,
                    Description = "Food",
                    Category = new Category { Id = 5, Name = "Groceries", Color = "#00FF00" }
                },
                new()
                {
                    Id = 2,
                    TransactionId = 1,
                    CategoryId = 6,
                    Amount = -40m,
                    Category = new Category { Id = 6, Name = "Household" }
                },
                new()
                {
                    Id = 3,
                    TransactionId = 1,
                    CategoryId = 7,
                    Amount = -999m,
                    IsDeleted = true // soft-deleted splits must not surface
                }
            }
        };
        _transactionRepository.GetByIdWithSplitsAsync(1, _userId).Returns(transaction);

        var result = await _handler.Handle(
            new GetTransactionQuery { UserId = _userId, Id = 1 }, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Splits.Should().NotBeNull();
        result.Splits.Should().HaveCount(2);

        var first = result.Splits!.Single(s => s.CategoryId == 5);
        first.Amount.Should().Be(-60m);
        first.Description.Should().Be("Food");
        first.CategoryName.Should().Be("Groceries");
        first.CategoryColor.Should().Be("#00FF00");

        result.Splits!.Single(s => s.CategoryId == 6).Amount.Should().Be(-40m);
    }

    [Fact]
    public async Task Handle_WhenTransactionHasNoSplits_ShouldReturnNullSplits()
    {
        var transaction = new Transaction
        {
            Id = 2,
            Amount = -50m,
            Description = "Coffee",
            AccountId = 10,
            Account = new Account { Id = 10, Name = "Checking", UserId = _userId }
        };
        _transactionRepository.GetByIdWithSplitsAsync(2, _userId).Returns(transaction);

        var result = await _handler.Handle(
            new GetTransactionQuery { UserId = _userId, Id = 2 }, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Splits.Should().BeNull();
    }
}
