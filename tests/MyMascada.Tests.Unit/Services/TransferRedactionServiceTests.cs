using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transfers.DTOs;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services;

namespace MyMascada.Tests.Unit.Services;

public class TransferRedactionServiceTests
{
    private readonly IAccountAccessService _accountAccess;
    private readonly TransferRedactionService _service;

    private readonly Guid _viewerUserId = Guid.NewGuid();

    private const int AccessibleAccountId1 = 1;
    private const int AccessibleAccountId2 = 2;
    private const int InaccessibleAccountId = 99;

    public TransferRedactionServiceTests()
    {
        _accountAccess = Substitute.For<IAccountAccessService>();

        _service = new TransferRedactionService(_accountAccess);

        // Default: viewer can access accounts 1 and 2
        var accessibleIds = new HashSet<int> { AccessibleAccountId1, AccessibleAccountId2 } as IReadOnlySet<int>;
        _accountAccess.GetAccessibleAccountIdsAsync(_viewerUserId).Returns(accessibleIds);
    }

    [Fact]
    public async Task RedactForViewerAsync_UserOwnsAllAccounts_NoRedaction()
    {
        // Arrange
        var transfer = CreateTransferDto(AccessibleAccountId1, AccessibleAccountId2);

        // Act
        var result = await _service.RedactForViewerAsync(transfer, _viewerUserId);

        // Assert
        result.SourceAccount.Id.Should().Be(AccessibleAccountId1);
        result.SourceAccount.Name.Should().Be("Source Account");
        result.DestinationAccount.Id.Should().Be(AccessibleAccountId2);
        result.DestinationAccount.Name.Should().Be("Destination Account");
        result.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task RedactForViewerAsync_SourceInaccessible_RedactsSourceDetails()
    {
        // Arrange
        var transfer = CreateTransferDto(InaccessibleAccountId, AccessibleAccountId1);

        // Act
        var result = await _service.RedactForViewerAsync(transfer, _viewerUserId);

        // Assert - source should be redacted
        result.SourceAccount.Id.Should().Be(0);
        result.SourceAccount.Name.Should().Be("Private Account");
        result.SourceAccount.Type.Should().BeEmpty();

        // Destination should remain intact
        result.DestinationAccount.Id.Should().Be(AccessibleAccountId1);
        result.DestinationAccount.Name.Should().Be("Destination Account");

        // Transactions for the inaccessible account should be filtered out
        result.Transactions.Should().ContainSingle()
            .Which.AccountId.Should().Be(AccessibleAccountId1);
    }

    [Fact]
    public async Task RedactForViewerAsync_DestinationInaccessible_RedactsDestinationDetails()
    {
        // Arrange
        var transfer = CreateTransferDto(AccessibleAccountId1, InaccessibleAccountId);

        // Act
        var result = await _service.RedactForViewerAsync(transfer, _viewerUserId);

        // Assert - destination should be redacted
        result.DestinationAccount.Id.Should().Be(0);
        result.DestinationAccount.Name.Should().Be("Private Account");
        result.DestinationAccount.Type.Should().BeEmpty();

        // Source should remain intact
        result.SourceAccount.Id.Should().Be(AccessibleAccountId1);
        result.SourceAccount.Name.Should().Be("Source Account");

        // Transactions for the inaccessible account should be filtered out
        result.Transactions.Should().ContainSingle()
            .Which.AccountId.Should().Be(AccessibleAccountId1);
    }

    [Fact]
    public async Task RedactForViewerAsync_BothInaccessible_RedactsBothSides()
    {
        // Arrange
        var otherInaccessibleId = 100;
        var transfer = CreateTransferDto(InaccessibleAccountId, otherInaccessibleId);

        // Act
        var result = await _service.RedactForViewerAsync(transfer, _viewerUserId);

        // Assert - both sides should be redacted
        result.SourceAccount.Id.Should().Be(0);
        result.SourceAccount.Name.Should().Be("Private Account");
        result.DestinationAccount.Id.Should().Be(0);
        result.DestinationAccount.Name.Should().Be("Private Account");
        result.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task RedactForViewerAsync_Collection_RedactsEachTransfer()
    {
        // Arrange
        var transfers = new List<TransferDto>
        {
            CreateTransferDto(AccessibleAccountId1, AccessibleAccountId2),
            CreateTransferDto(InaccessibleAccountId, AccessibleAccountId1)
        };

        // Act
        var result = (await _service.RedactForViewerAsync(transfers, _viewerUserId)).ToList();

        // Assert
        result.Should().HaveCount(2);

        // First transfer: both accessible, no redaction
        result[0].SourceAccount.Id.Should().Be(AccessibleAccountId1);
        result[0].DestinationAccount.Id.Should().Be(AccessibleAccountId2);

        // Second transfer: source inaccessible, should be redacted
        result[1].SourceAccount.Id.Should().Be(0);
        result[1].SourceAccount.Name.Should().Be("Private Account");
        result[1].DestinationAccount.Id.Should().Be(AccessibleAccountId1);
    }

    private static TransferDto CreateTransferDto(int sourceAccountId, int destinationAccountId)
    {
        return new TransferDto
        {
            Id = 1,
            TransferId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = TransferStatus.Completed,
            TransferDate = DateTime.UtcNow,
            SourceAccount = new TransferAccountDto
            {
                Id = sourceAccountId,
                Name = "Source Account",
                Currency = "USD",
                Type = "Checking"
            },
            DestinationAccount = new TransferAccountDto
            {
                Id = destinationAccountId,
                Name = "Destination Account",
                Currency = "USD",
                Type = "Savings"
            },
            Transactions = new List<TransferTransactionDto>
            {
                new()
                {
                    Id = 1,
                    Amount = -100m,
                    Description = "Transfer out",
                    IsTransferSource = true,
                    AccountId = sourceAccountId,
                    Type = TransactionType.TransferComponent
                },
                new()
                {
                    Id = 2,
                    Amount = 100m,
                    Description = "Transfer in",
                    IsTransferSource = false,
                    AccountId = destinationAccountId,
                    Type = TransactionType.TransferComponent
                }
            }
        };
    }
}
