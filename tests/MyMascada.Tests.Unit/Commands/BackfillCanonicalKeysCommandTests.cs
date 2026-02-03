using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.Commands;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Commands;

public class BackfillCanonicalKeysCommandTests
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IApplicationLogger<BackfillCanonicalKeysCommandHandler> _logger;
    private readonly BackfillCanonicalKeysCommandHandler _handler;

    public BackfillCanonicalKeysCommandTests()
    {
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _logger = Substitute.For<IApplicationLogger<BackfillCanonicalKeysCommandHandler>>();

        _handler = new BackfillCanonicalKeysCommandHandler(
            _categoryRepository,
            _logger);
    }

    [Fact]
    public async Task Handle_WhenNoCategoriesHaveNullCanonicalKey_ReturnsZeroCounts()
    {
        // Arrange
        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(new List<Category>());

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UsersProcessed.Should().Be(0);
        result.CategoriesUpdated.Should().Be(0);
        await _categoryRepository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenCategoriesMatchEnglishNames_SetsCanonicalKeys()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "Income", UserId = userId, CanonicalKey = null },
            new Category { Id = 2, Name = "Groceries", UserId = userId, CanonicalKey = null },
            new Category { Id = 3, Name = "Salary", UserId = userId, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.UsersProcessed.Should().Be(1);
        result.CategoriesUpdated.Should().Be(3);
        categories[0].CanonicalKey.Should().Be("income");
        categories[1].CanonicalKey.Should().Be("groceries");
        categories[2].CanonicalKey.Should().Be("salary");
        await _categoryRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenCategoryNameDoesNotMatch_LeavesCanonicalKeyNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "My Custom Category", UserId = userId, CanonicalKey = null },
            new Category { Id = 2, Name = "Income", UserId = userId, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CategoriesUpdated.Should().Be(1);
        categories[0].CanonicalKey.Should().BeNull(); // Custom category not matched
        categories[1].CanonicalKey.Should().Be("income"); // Seeded category matched
    }

    [Fact]
    public async Task Handle_MatchesCaseInsensitively()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "income", UserId = userId, CanonicalKey = null },
            new Category { Id = 2, Name = "GROCERIES", UserId = userId, CanonicalKey = null },
            new Category { Id = 3, Name = "Housing & Utilities", UserId = userId, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CategoriesUpdated.Should().Be(3);
        categories[0].CanonicalKey.Should().Be("income");
        categories[1].CanonicalKey.Should().Be("groceries");
        categories[2].CanonicalKey.Should().Be("housing_utilities");
    }

    [Fact]
    public async Task Handle_CountsDistinctUsersProcessed()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "Income", UserId = userId1, CanonicalKey = null },
            new Category { Id = 2, Name = "Salary", UserId = userId1, CanonicalKey = null },
            new Category { Id = 3, Name = "Income", UserId = userId2, CanonicalKey = null },
            new Category { Id = 4, Name = "Groceries", UserId = userId2, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.UsersProcessed.Should().Be(2);
        result.CategoriesUpdated.Should().Be(4);
    }

    [Fact]
    public async Task Handle_WhenMixOfMatchedAndUnmatched_OnlyUpdatesMatched()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "Income", UserId = userId, CanonicalKey = null },
            new Category { Id = 2, Name = "User Custom 1", UserId = userId, CanonicalKey = null },
            new Category { Id = 3, Name = "Restaurants", UserId = userId, CanonicalKey = null },
            new Category { Id = 4, Name = "User Custom 2", UserId = userId, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CategoriesUpdated.Should().Be(2);
        result.UsersProcessed.Should().Be(1);

        categories[0].CanonicalKey.Should().Be("income");
        categories[1].CanonicalKey.Should().BeNull();
        categories[2].CanonicalKey.Should().Be("restaurants");
        categories[3].CanonicalKey.Should().BeNull();

        await _categoryRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenOnlyUnmatchedCategories_DoesNotCallSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "My Custom Category", UserId = userId, CanonicalKey = null },
            new Category { Id = 2, Name = "Another Custom", UserId = userId, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CategoriesUpdated.Should().Be(0);
        result.UsersProcessed.Should().Be(0);
        await _categoryRepository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_SetsUpdatedAtForMatchedCategories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalUpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var category = new Category
        {
            Id = 1,
            Name = "Income",
            UserId = userId,
            CanonicalKey = null,
            UpdatedAt = originalUpdatedAt
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(new List<Category> { category });

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var beforeExecution = DateTime.UtcNow;
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        category.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        category.UpdatedAt.Should().NotBe(originalUpdatedAt);
    }

    [Fact]
    public async Task Handle_AllParentCategoryNames_AreRecognized()
    {
        // Arrange: Test all 15 parent category names
        var userId = Guid.NewGuid();
        var parentNames = new[]
        {
            "Income", "Housing & Utilities", "Transportation", "Food & Dining",
            "Health & Medical", "Personal Care", "Entertainment", "Travel & Vacation",
            "Financial Services", "Debt Payments", "Technology", "Family & Children",
            "Gifts & Donations", "Miscellaneous", "Transfers"
        };

        var categories = parentNames.Select((name, i) => new Category
        {
            Id = i + 1,
            Name = name,
            UserId = userId,
            CanonicalKey = null
        }).ToList();

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CategoriesUpdated.Should().Be(15);
        categories.All(c => c.CanonicalKey != null).Should().BeTrue("all parent category names should be recognized");
    }

    [Fact]
    public async Task Handle_CategoryWithNullUserId_DoesNotCountAsUser()
    {
        // Arrange: System categories may have null UserId
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "Income", UserId = null, CanonicalKey = null }
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(categories);

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.CategoriesUpdated.Should().Be(1);
        result.UsersProcessed.Should().Be(0); // System categories have null UserId
        categories[0].CanonicalKey.Should().Be("income");
    }

    [Theory]
    [InlineData("Gas", "gas_utility")]
    [InlineData("Gas & Fuel", "gas_fuel")]
    public async Task Handle_DistinguishesSimilarNames_Correctly(string categoryName, string expectedKey)
    {
        // Arrange: "Gas" (utility) and "Gas & Fuel" (transportation) are distinct
        var userId = Guid.NewGuid();
        var category = new Category
        {
            Id = 1,
            Name = categoryName,
            UserId = userId,
            CanonicalKey = null
        };

        _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync()
            .Returns(new List<Category> { category });

        var command = new BackfillCanonicalKeysCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        category.CanonicalKey.Should().Be(expectedKey);
    }
}
