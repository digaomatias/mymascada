using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.DescriptionCleaning.Commands;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Services;

public class CleanTransactionDescriptionsCommandTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IDescriptionCleaningService _descriptionCleaningService;
    private readonly ILogger<CleanTransactionDescriptionsCommandHandler> _logger;
    private readonly CleanTransactionDescriptionsCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CleanTransactionDescriptionsCommandTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();
        _descriptionCleaningService = Substitute.For<IDescriptionCleaningService>();
        _logger = Substitute.For<ILogger<CleanTransactionDescriptionsCommandHandler>>();

        _handler = new CleanTransactionDescriptionsCommandHandler(
            _transactionRepository,
            _accountAccessService,
            _descriptionCleaningService,
            _logger);

        // Default: allow access
        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns(true);
    }

    [Fact]
    public async Task Handle_WithNoTransactions_ReturnsEmptyResult()
    {
        // Arrange
        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Transaction>());

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1, 2, 3 }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("No valid transactions found for the provided IDs");
        result.ProcessedTransactions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SkipsTransactionsWithExistingUserDescription()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Id = 1,
                Description = "POS 4829 COUNTDOWN AUCKLAND NZ",
                UserDescription = "Countdown",
                AccountId = 1
            },
            new Transaction
            {
                Id = 2,
                Description = "AMZN MKTP US*RT4K92JF0",
                UserDescription = null,
                AccountId = 1
            }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        _descriptionCleaningService.CleanDescriptionsAsync(
            Arg.Any<IEnumerable<DescriptionCleaningInput>>(), Arg.Any<CancellationToken>())
            .Returns(new DescriptionCleaningResponse
            {
                Success = true,
                Results = new List<CleanedDescription>
                {
                    new CleanedDescription
                    {
                        TransactionId = 2,
                        OriginalDescription = "AMZN MKTP US*RT4K92JF0",
                        Description = "Amazon Marketplace",
                        Confidence = 0.95m,
                        Reasoning = "Extracted Amazon merchant name"
                    }
                }
            });

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1, 2 }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SkippedTransactions.Should().Be(1);
        result.ProcessedTransactions.Should().Be(1);

        // Verify only the eligible transaction was sent to the cleaning service
        await _descriptionCleaningService.Received(1).CleanDescriptionsAsync(
            Arg.Is<IEnumerable<DescriptionCleaningInput>>(inputs =>
                inputs.Count() == 1 && inputs.First().TransactionId == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AppliesCleanedDescriptionsAboveConfidenceThreshold()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Id = 1,
                Description = "POS 4829 COUNTDOWN AUCKLAND NZ",
                AccountId = 1
            },
            new Transaction
            {
                Id = 2,
                Description = "UNKNOWN REF 123",
                AccountId = 1
            }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        _descriptionCleaningService.CleanDescriptionsAsync(
            Arg.Any<IEnumerable<DescriptionCleaningInput>>(), Arg.Any<CancellationToken>())
            .Returns(new DescriptionCleaningResponse
            {
                Success = true,
                Results = new List<CleanedDescription>
                {
                    new CleanedDescription
                    {
                        TransactionId = 1,
                        OriginalDescription = "POS 4829 COUNTDOWN AUCKLAND NZ",
                        Description = "Countdown",
                        Confidence = 0.95m,
                        Reasoning = "Clear merchant name"
                    },
                    new CleanedDescription
                    {
                        TransactionId = 2,
                        OriginalDescription = "UNKNOWN REF 123",
                        Description = "Unknown",
                        Confidence = 0.3m,
                        Reasoning = "Ambiguous description"
                    }
                }
            });

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1, 2 },
            ConfidenceThreshold = 0.7m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CleanedTransactions.Should().Be(1);
        result.Previews.Should().HaveCount(2);

        // Only transaction 1 should have been updated (confidence 0.95 >= 0.7 threshold)
        await _transactionRepository.Received(1).UpdateAsync(
            Arg.Is<Transaction>(t => t.Id == 1 && t.UserDescription == "Countdown"));

        // Transaction 2 should NOT have been updated (confidence 0.3 < 0.7 threshold)
        await _transactionRepository.DidNotReceive().UpdateAsync(
            Arg.Is<Transaction>(t => t.Id == 2));
    }

    [Fact]
    public async Task Handle_WithUnauthorizedAccess_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new Transaction { Id = 1, Description = "Test", AccountId = 99 }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), 99)
            .Returns(false);

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1 }
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAllTransactionsHaveUserDescription_ReturnsSkippedMessage()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Id = 1,
                Description = "POS 4829 COUNTDOWN",
                UserDescription = "Countdown",
                AccountId = 1
            }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1 }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SkippedTransactions.Should().Be(1);
        result.Message.Should().Be("All transactions already have user descriptions");

        // Cleaning service should not be called
        await _descriptionCleaningService.DidNotReceive().CleanDescriptionsAsync(
            Arg.Any<IEnumerable<DescriptionCleaningInput>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExtractsMerchantNameHintFromNotes()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Id = 1,
                Description = "POS 4829 SOME STORE",
                Notes = "Merchant: The Coffee Shop",
                AccountId = 1
            }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        _descriptionCleaningService.CleanDescriptionsAsync(
            Arg.Any<IEnumerable<DescriptionCleaningInput>>(), Arg.Any<CancellationToken>())
            .Returns(new DescriptionCleaningResponse
            {
                Success = true,
                Results = new List<CleanedDescription>
                {
                    new CleanedDescription
                    {
                        TransactionId = 1,
                        OriginalDescription = "POS 4829 SOME STORE",
                        Description = "The Coffee Shop",
                        Confidence = 0.95m,
                        Reasoning = "Used merchant name hint"
                    }
                }
            });

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1 }
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - verify merchant name hint was passed to the service
        await _descriptionCleaningService.Received(1).CleanDescriptionsAsync(
            Arg.Is<IEnumerable<DescriptionCleaningInput>>(inputs =>
                inputs.First().MerchantNameHint == "The Coffee Shop"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServiceFails_ReturnsErrors()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new Transaction { Id = 1, Description = "Test", AccountId = 1 }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        _descriptionCleaningService.CleanDescriptionsAsync(
            Arg.Any<IEnumerable<DescriptionCleaningInput>>(), Arg.Any<CancellationToken>())
            .Returns(new DescriptionCleaningResponse
            {
                Success = false,
                Errors = new List<string> { "LLM service unavailable" }
            });

        var command = new CleanTransactionDescriptionsCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1 }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("LLM service unavailable");
    }
}
