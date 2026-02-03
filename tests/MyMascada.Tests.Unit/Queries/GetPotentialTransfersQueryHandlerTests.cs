using AutoMapper;
using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Queries;

public class GetPotentialTransfersQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IMapper _mapper;
    private readonly GetPotentialTransfersQueryHandler _handler;

    public GetPotentialTransfersQueryHandlerTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _mapper = Substitute.For<IMapper>();
        _handler = new GetPotentialTransfersQueryHandler(_transactionRepository, _accountRepository, _mapper);
    }

    [Fact]
    public async Task Handle_WhenOutgoingAndIncomingTransactionsMatch_ShouldSetCorrectTransferDirection()
    {
        // Arrange - Create a transfer scenario: outgoing from Account 1, incoming to Account 2
        var userId = Guid.NewGuid();
        var outgoingTransaction = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -500.00m, // Outgoing (negative)
            TransactionDate = DateTime.Today,
            Description = "Transfer to savings"
        };
        
        var incomingTransaction = new Transaction
        {
            Id = 2,
            AccountId = 2,
            Amount = 500.00m, // Incoming (positive)
            TransactionDate = DateTime.Today,
            Description = "Transfer from checking"
        };

        var transactions = new List<Transaction> { outgoingTransaction, incomingTransaction };
        
        var outgoingDto = new TransactionDto
        {
            Id = 1,
            AccountId = 1,
            Amount = -500.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer to savings",
            AccountName = "Checking Account"
        };
        
        var incomingDto = new TransactionDto
        {
            Id = 2,
            AccountId = 2,
            Amount = 500.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer from checking",
            AccountName = "Savings Account"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);
        
        _mapper.Map<TransactionDto>(outgoingTransaction).Returns(outgoingDto);
        _mapper.Map<TransactionDto>(incomingTransaction).Returns(incomingDto);

        var query = new GetPotentialTransfersQuery
        {
            UserId = userId,
            IncludeReviewed = false,
            IncludeExistingTransfers = false
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TransferGroups.Should().HaveCount(1);
        var transferGroup = result.TransferGroups.First();
        
        // CRITICAL: Verify transfer direction is based on money flow, not iteration order
        transferGroup.SourceTransaction.Amount.Should().Be(-500.00m, "source should be the outgoing (negative) transaction");
        transferGroup.DestinationTransaction.Amount.Should().Be(500.00m, "destination should be the incoming (positive) transaction");
        transferGroup.SourceTransaction.Id.Should().Be(1, "outgoing transaction should be the source");
        transferGroup.DestinationTransaction.Id.Should().Be(2, "incoming transaction should be the destination");
    }

    [Fact]
    public async Task Handle_WhenIncomingProcessedFirst_ShouldStillSetCorrectTransferDirection()
    {
        // Arrange - Process incoming transaction first to test direction logic robustness
        var userId = Guid.NewGuid();
        var outgoingTransaction = new Transaction
        {
            Id = 10,
            AccountId = 1,
            Amount = -1000.00m, // Outgoing
            TransactionDate = DateTime.Today,
            Description = "Transfer out"
        };
        
        var incomingTransaction = new Transaction
        {
            Id = 20,
            AccountId = 2,  
            Amount = 1000.00m, // Incoming
            TransactionDate = DateTime.Today,
            Description = "Transfer in"
        };

        // Important: Put incoming transaction first in the list to test iteration order independence
        var transactions = new List<Transaction> { incomingTransaction, outgoingTransaction };
        
        var outgoingDto = new TransactionDto
        {
            Id = 10,
            AccountId = 1,
            Amount = -1000.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer out",
            AccountName = "Account A"
        };
        
        var incomingDto = new TransactionDto
        {
            Id = 20,
            AccountId = 2,
            Amount = 1000.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer in",
            AccountName = "Account B"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);
        
        _mapper.Map<TransactionDto>(outgoingTransaction).Returns(outgoingDto);
        _mapper.Map<TransactionDto>(incomingTransaction).Returns(incomingDto);

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TransferGroups.Should().HaveCount(1);
        var transferGroup = result.TransferGroups.First();
        
        // CRITICAL: Direction should be correct regardless of processing order
        transferGroup.SourceTransaction.Id.Should().Be(10, "outgoing transaction should always be source");
        transferGroup.DestinationTransaction.Id.Should().Be(20, "incoming transaction should always be destination");
        transferGroup.SourceTransaction.Amount.Should().BeNegative("source should be outgoing");
        transferGroup.DestinationTransaction.Amount.Should().BePositive("destination should be incoming");
    }

    [Fact]
    public async Task Handle_WhenBothTransactionsAreSameDirection_ShouldNotCreateTransfer()
    {
        // Arrange - Both transactions are outgoing (invalid transfer scenario)
        var userId = Guid.NewGuid();
        var transaction1 = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -500.00m, // Outgoing
            TransactionDate = DateTime.Today,
            Description = "Purchase"
        };
        
        var transaction2 = new Transaction
        {
            Id = 2,
            AccountId = 2,
            Amount = -500.00m, // Also outgoing - NOT a transfer
            TransactionDate = DateTime.Today,
            Description = "Another purchase"
        };

        var transactions = new List<Transaction> { transaction1, transaction2 };
        
        var dto1 = new TransactionDto
        {
            Id = 1,
            AccountId = 1,
            Amount = -500.00m,
            TransactionDate = DateTime.Today,
            Description = "Purchase"
        };
        
        var dto2 = new TransactionDto
        {
            Id = 2,
            AccountId = 2,
            Amount = -500.00m,
            TransactionDate = DateTime.Today,
            Description = "Another purchase"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);
        
        _mapper.Map<TransactionDto>(transaction1).Returns(dto1);
        _mapper.Map<TransactionDto>(transaction2).Returns(dto2);

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TransferGroups.Should().BeEmpty("two transactions in the same direction cannot form a transfer");
    }

    [Fact]
    public async Task Handle_WhenBothTransactionsAreIncoming_ShouldNotCreateTransfer()
    {
        // Arrange - Both transactions are incoming (also invalid transfer scenario)
        var userId = Guid.NewGuid();
        var transaction1 = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = 500.00m, // Incoming
            TransactionDate = DateTime.Today,
            Description = "Deposit"
        };
        
        var transaction2 = new Transaction
        {
            Id = 2,
            AccountId = 2,
            Amount = 500.00m, // Also incoming - NOT a transfer
            TransactionDate = DateTime.Today,
            Description = "Another deposit"
        };

        var transactions = new List<Transaction> { transaction1, transaction2 };
        
        var dto1 = new TransactionDto
        {
            Id = 1,
            AccountId = 1,
            Amount = 500.00m,
            TransactionDate = DateTime.Today,
            Description = "Deposit"
        };
        
        var dto2 = new TransactionDto
        {
            Id = 2,
            AccountId = 2,
            Amount = 500.00m,
            TransactionDate = DateTime.Today,
            Description = "Another deposit"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);
        
        _mapper.Map<TransactionDto>(transaction1).Returns(dto1);
        _mapper.Map<TransactionDto>(transaction2).Returns(dto2);

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TransferGroups.Should().BeEmpty("two incoming transactions cannot form a transfer");
    }

    [Fact]
    public async Task Handle_WithMultipleTransferPairs_ShouldSetCorrectDirectionForEach()
    {
        // Arrange - Multiple transfer pairs to ensure consistent direction logic
        var userId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            // Transfer 1: -$200 out of Account 1, +$200 into Account 2
            new() { Id = 1, AccountId = 1, Amount = -200.00m, TransactionDate = DateTime.Today, Description = "Transfer 1 out" },
            new() { Id = 2, AccountId = 2, Amount = 200.00m, TransactionDate = DateTime.Today, Description = "Transfer 1 in" },
            
            // Transfer 2: +$300 into Account 3, -$300 out of Account 4 (different order)
            new() { Id = 3, AccountId = 3, Amount = 300.00m, TransactionDate = DateTime.Today.AddDays(-1), Description = "Transfer 2 in" },
            new() { Id = 4, AccountId = 4, Amount = -300.00m, TransactionDate = DateTime.Today.AddDays(-1), Description = "Transfer 2 out" },
        };

        var dtos = transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            AccountId = t.AccountId,
            Amount = t.Amount,
            TransactionDate = t.TransactionDate,
            Description = t.Description
        }).ToList();

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);

        foreach (var (transaction, dto) in transactions.Zip(dtos))
        {
            _mapper.Map<TransactionDto>(transaction).Returns(dto);
        }

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TransferGroups.Should().HaveCount(2, "should detect both transfer pairs");
        
        foreach (var group in result.TransferGroups)
        {
            group.SourceTransaction.Amount.Should().BeNegative("source should always be the outgoing transaction");
            group.DestinationTransaction.Amount.Should().BePositive("destination should always be the incoming transaction");
        }

        // Verify specific transfers
        var transfer1 = result.TransferGroups.FirstOrDefault(g => 
            g.SourceTransaction.Id == 1 && g.DestinationTransaction.Id == 2);
        transfer1.Should().NotBeNull("should correctly match transfer 1");

        var transfer2 = result.TransferGroups.FirstOrDefault(g =>
            g.SourceTransaction.Id == 4 && g.DestinationTransaction.Id == 3);
        transfer2.Should().NotBeNull("should correctly match transfer 2 regardless of processing order");
    }

    [Fact]
    public async Task Handle_WhenAmountsDiffer_ShouldNotMatchAsTransfer()
    {
        // Arrange - Scenario from bug report: $4.99 vs $5.29
        // Transfers require EXACT amount match - no tolerance
        var userId = Guid.NewGuid();
        var transaction1 = new Transaction
        {
            Id = 969,
            AccountId = 1,
            Amount = -4.99m, // AMEX charge
            TransactionDate = new DateTime(2025, 8, 30),
            Description = "APPLE.COM/BILL SYDNEY"
        };

        var transaction2 = new Transaction
        {
            Id = 991,
            AccountId = 2,
            Amount = 5.29m, // ANZ Savings deposit - different amount!
            TransactionDate = new DateTime(2025, 8, 29),
            Description = "Deposit"
        };

        var transactions = new List<Transaction> { transaction1, transaction2 };

        var dto1 = new TransactionDto
        {
            Id = 969,
            AccountId = 1,
            Amount = -4.99m,
            TransactionDate = new DateTime(2025, 8, 30),
            Description = "APPLE.COM/BILL SYDNEY",
            AccountName = "AMEX"
        };

        var dto2 = new TransactionDto
        {
            Id = 991,
            AccountId = 2,
            Amount = 5.29m,
            TransactionDate = new DateTime(2025, 8, 29),
            Description = "Deposit",
            AccountName = "ANZ Savings"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);

        _mapper.Map<TransactionDto>(transaction1).Returns(dto1);
        _mapper.Map<TransactionDto>(transaction2).Returns(dto2);

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Different amounts should NOT match as transfer
        result.TransferGroups.Should().BeEmpty(
            "transactions with different amounts ($4.99 vs $5.29) should not be matched as transfers");
    }

    [Fact]
    public async Task Handle_WhenAmountsMatchExactly_ShouldMatchAsTransfer()
    {
        // Arrange - Exact match should work
        var userId = Guid.NewGuid();
        var transaction1 = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -100.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        var transaction2 = new Transaction
        {
            Id = 2,
            AccountId = 2,
            Amount = 100.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        var transactions = new List<Transaction> { transaction1, transaction2 };

        var dto1 = new TransactionDto
        {
            Id = 1,
            AccountId = 1,
            Amount = -100.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        var dto2 = new TransactionDto
        {
            Id = 2,
            AccountId = 2,
            Amount = 100.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);

        _mapper.Map<TransactionDto>(transaction1).Returns(dto1);
        _mapper.Map<TransactionDto>(transaction2).Returns(dto2);

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TransferGroups.Should().HaveCount(1, "exact amount matches should be matched as transfers");
    }

    [Fact]
    public async Task Handle_WhenAmountsDifferByOneCent_ShouldNotMatchAsTransfer()
    {
        // Arrange - Even $0.01 difference should NOT match (no tolerance)
        var userId = Guid.NewGuid();
        var transaction1 = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -100.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        var transaction2 = new Transaction
        {
            Id = 2,
            AccountId = 2,
            Amount = 100.01m, // Just 1 cent different
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        var transactions = new List<Transaction> { transaction1, transaction2 };

        var dto1 = new TransactionDto
        {
            Id = 1,
            AccountId = 1,
            Amount = -100.00m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        var dto2 = new TransactionDto
        {
            Id = 2,
            AccountId = 2,
            Amount = 100.01m,
            TransactionDate = DateTime.Today,
            Description = "Transfer"
        };

        _transactionRepository.GetUserTransactionsAsync(userId, false, false, false)
            .Returns(transactions);

        _mapper.Map<TransactionDto>(transaction1).Returns(dto1);
        _mapper.Map<TransactionDto>(transaction2).Returns(dto2);

        var query = new GetPotentialTransfersQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Even 1 cent difference should NOT match
        result.TransferGroups.Should().BeEmpty(
            "transfers require exact amount match - even $0.01 difference should not match");
    }
}