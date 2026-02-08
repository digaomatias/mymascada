using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Services;

namespace MyMascada.Tests.Unit.Services;

public class AccountAccessServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IFeatureFlags _featureFlags;
    private readonly AccountAccessService _service;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _shareeId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    private const int OwnedAccountId = 1;
    private const int SharedAccountId = 2;
    private const int UnknownAccountId = 999;

    public AccountAccessServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _featureFlags = Substitute.For<IFeatureFlags>();

        _service = new AccountAccessService(_context, _featureFlags);

        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Create users
        var owner = new User
        {
            Id = _ownerId,
            Email = "owner@example.com",
            PasswordHash = "hash",
            FirstName = "Owner",
            LastName = "User"
        };

        var sharee = new User
        {
            Id = _shareeId,
            Email = "sharee@example.com",
            PasswordHash = "hash",
            FirstName = "Sharee",
            LastName = "User"
        };

        _context.Users.AddRange(owner, sharee);

        // Account owned by owner
        var ownedAccount = new Account
        {
            Id = OwnedAccountId,
            UserId = _ownerId,
            Name = "Owner Checking",
            Type = AccountType.Checking,
            CurrentBalance = 1000m
        };

        // Account owned by stranger, shared with sharee
        var sharedAccount = new Account
        {
            Id = SharedAccountId,
            UserId = _strangerId,
            Name = "Stranger Savings",
            Type = AccountType.Savings,
            CurrentBalance = 5000m
        };

        _context.Accounts.AddRange(ownedAccount, sharedAccount);

        // Accepted share: stranger's account shared with sharee as Viewer
        var viewerShare = new AccountShare
        {
            Id = 1,
            AccountId = SharedAccountId,
            SharedWithUserId = _shareeId,
            SharedByUserId = _strangerId,
            Role = AccountShareRole.Viewer,
            Status = AccountShareStatus.Accepted
        };

        _context.AccountShares.Add(viewerShare);

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAccessibleAccountIdsAsync_WithoutSharing_ReturnsOnlyOwnedAccounts()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        // Act
        var result = await _service.GetAccessibleAccountIdsAsync(_ownerId);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(OwnedAccountId);
    }

    [Fact]
    public async Task GetAccessibleAccountIdsAsync_WithSharing_ReturnsOwnedAndSharedAccounts()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Add a share: owner's account shared with sharee (so sharee can see both owned + shared)
        _context.AccountShares.Add(new AccountShare
        {
            Id = 10,
            AccountId = OwnedAccountId,
            SharedWithUserId = _shareeId,
            SharedByUserId = _ownerId,
            Role = AccountShareRole.Viewer,
            Status = AccountShareStatus.Accepted
        });
        await _context.SaveChangesAsync();

        // Act - sharee has no owned accounts but has two shared accounts
        var result = await _service.GetAccessibleAccountIdsAsync(_shareeId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(OwnedAccountId);
        result.Should().Contain(SharedAccountId);
    }

    [Fact]
    public async Task CanAccessAccountAsync_OwnedAccount_ReturnsTrue()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        // Act
        var result = await _service.CanAccessAccountAsync(_ownerId, OwnedAccountId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAccountAsync_SharedAccount_ReturnsTrue()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Act - sharee has an accepted share on SharedAccountId
        var result = await _service.CanAccessAccountAsync(_shareeId, SharedAccountId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAccountAsync_UnknownAccount_ReturnsFalse()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Act
        var result = await _service.CanAccessAccountAsync(_ownerId, UnknownAccountId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanModifyAccountAsync_OwnedAccount_ReturnsTrue()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        // Act
        var result = await _service.CanModifyAccountAsync(_ownerId, OwnedAccountId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanModifyAccountAsync_ManagerRole_ReturnsTrue()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Add a Manager share
        _context.AccountShares.Add(new AccountShare
        {
            Id = 20,
            AccountId = OwnedAccountId,
            SharedWithUserId = _shareeId,
            SharedByUserId = _ownerId,
            Role = AccountShareRole.Manager,
            Status = AccountShareStatus.Accepted
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CanModifyAccountAsync(_shareeId, OwnedAccountId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanModifyAccountAsync_ViewerRole_ReturnsFalse()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // sharee has a Viewer share on SharedAccountId (seeded in setup)

        // Act
        var result = await _service.CanModifyAccountAsync(_shareeId, SharedAccountId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanModifyAccountAsync_UnknownAccount_ReturnsFalse()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Act
        var result = await _service.CanModifyAccountAsync(_ownerId, UnknownAccountId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOwnerAsync_OwnedAccount_ReturnsTrue()
    {
        // Act
        var result = await _service.IsOwnerAsync(_ownerId, OwnedAccountId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerAsync_SharedAccount_ReturnsFalse()
    {
        // Act - sharee is not the owner of SharedAccountId
        var result = await _service.IsOwnerAsync(_shareeId, SharedAccountId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAccessibleAccountIdsAsync_CalledTwice_UsesCacheOnSecondCall()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Act - call twice for the same user
        var result1 = await _service.GetAccessibleAccountIdsAsync(_ownerId);
        var result2 = await _service.GetAccessibleAccountIdsAsync(_ownerId);

        // Assert
        // Both results should be the same reference (cached)
        result1.Should().BeSameAs(result2);

        // Additionally verify the owned account is present
        result1.Should().Contain(OwnedAccountId);
    }

    [Fact]
    public async Task GetAccessibleAccountIdsAsync_WithSharing_ExcludesPendingShares()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        // Add a pending share (should not grant access)
        var pendingAccount = new Account
        {
            Id = 50,
            UserId = _strangerId,
            Name = "Pending Account",
            Type = AccountType.Checking,
            CurrentBalance = 100m
        };
        _context.Accounts.Add(pendingAccount);

        _context.AccountShares.Add(new AccountShare
        {
            Id = 30,
            AccountId = 50,
            SharedWithUserId = _shareeId,
            SharedByUserId = _strangerId,
            Role = AccountShareRole.Viewer,
            Status = AccountShareStatus.Pending
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAccessibleAccountIdsAsync(_shareeId);

        // Assert - sharee should only see SharedAccountId (accepted), not account 50 (pending)
        result.Should().Contain(SharedAccountId);
        result.Should().NotContain(50);
    }

    [Fact]
    public async Task GetAccountOwnerIdAsync_ExistingAccount_ReturnsOwnerId()
    {
        // Act
        var result = await _service.GetAccountOwnerIdAsync(OwnedAccountId);

        // Assert
        result.Should().Be(_ownerId);
    }

    [Fact]
    public async Task GetAccountOwnerIdAsync_NonExistentAccount_ReturnsNull()
    {
        // Act
        var result = await _service.GetAccountOwnerIdAsync(UnknownAccountId);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
