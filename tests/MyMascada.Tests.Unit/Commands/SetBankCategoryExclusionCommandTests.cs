using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Commands;

public class SetBankCategoryExclusionCommandTests
{
    private readonly IBankCategoryMappingRepository _mappingRepository;
    private readonly SetBankCategoryExclusionCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public SetBankCategoryExclusionCommandTests()
    {
        _mappingRepository = Substitute.For<IBankCategoryMappingRepository>();
        _handler = new SetBankCategoryExclusionCommandHandler(_mappingRepository);
    }

    [Fact]
    public async Task Handle_WhenValidMappingAndExcludeTrue_ShouldUpdateIsExcludedToTrue()
    {
        // Arrange: Valid mapping ID and user
        // Business Scenario: User wants to exclude "Lending Services" category from auto-categorization
        // because it incorrectly categorizes loan payments
        var mappingId = 1;
        var existingMapping = CreateMapping(
            id: mappingId,
            bankCategory: "Lending Services",
            categoryId: 100,
            isExcluded: false);

        _mappingRepository.GetByIdAsync(mappingId, _userId, Arg.Any<CancellationToken>())
            .Returns(existingMapping);

        var command = new SetBankCategoryExclusionCommand
        {
            MappingId = mappingId,
            UserId = _userId,
            IsExcluded = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Mapping updated with IsExcluded = true, returns updated DTO
        result.Should().NotBeNull();
        result!.Id.Should().Be(mappingId);
        result.IsExcluded.Should().BeTrue("mapping should now be excluded");
        result.BankCategoryName.Should().Be("Lending Services");

        // Verify UpdateAsync was called with the modified mapping
        await _mappingRepository.Received(1).UpdateAsync(
            Arg.Is<BankCategoryMapping>(m => m.Id == mappingId && m.IsExcluded == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidMappingAndExcludeFalse_ShouldUpdateIsExcludedToFalse()
    {
        // Arrange: User wants to re-enable auto-categorization for a previously excluded mapping
        // Business Scenario: User changed their mind and wants "Lending Services" to be used again
        var mappingId = 2;
        var existingMapping = CreateMapping(
            id: mappingId,
            bankCategory: "Lending Services",
            categoryId: 100,
            isExcluded: true); // Currently excluded

        _mappingRepository.GetByIdAsync(mappingId, _userId, Arg.Any<CancellationToken>())
            .Returns(existingMapping);

        var command = new SetBankCategoryExclusionCommand
        {
            MappingId = mappingId,
            UserId = _userId,
            IsExcluded = false // Turning off exclusion
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Mapping updated with IsExcluded = false
        result.Should().NotBeNull();
        result!.Id.Should().Be(mappingId);
        result.IsExcluded.Should().BeFalse("mapping should no longer be excluded");

        // Verify UpdateAsync was called
        await _mappingRepository.Received(1).UpdateAsync(
            Arg.Is<BankCategoryMapping>(m => m.Id == mappingId && m.IsExcluded == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMappingNotFound_ShouldReturnNull()
    {
        // Arrange: Invalid mapping ID that doesn't exist
        var invalidMappingId = 999;

        _mappingRepository.GetByIdAsync(invalidMappingId, _userId, Arg.Any<CancellationToken>())
            .Returns((BankCategoryMapping?)null);

        var command = new SetBankCategoryExclusionCommand
        {
            MappingId = invalidMappingId,
            UserId = _userId,
            IsExcluded = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Returns null
        result.Should().BeNull("mapping does not exist");

        // Verify UpdateAsync was NOT called
        await _mappingRepository.DidNotReceive().UpdateAsync(
            Arg.Any<BankCategoryMapping>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMappingBelongsToDifferentUser_ShouldReturnNull()
    {
        // Arrange: Mapping exists but belongs to different user
        // Business Scenario: Security check - users can only modify their own mappings
        var mappingId = 3;
        var differentUserId = Guid.NewGuid();

        // GetByIdAsync with wrong user returns null (security feature)
        _mappingRepository.GetByIdAsync(mappingId, differentUserId, Arg.Any<CancellationToken>())
            .Returns((BankCategoryMapping?)null);

        var command = new SetBankCategoryExclusionCommand
        {
            MappingId = mappingId,
            UserId = differentUserId, // Different user trying to access
            IsExcluded = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Returns null because mapping not found for this user
        result.Should().BeNull("mapping should not be accessible to different user");

        // Verify UpdateAsync was NOT called
        await _mappingRepository.DidNotReceive().UpdateAsync(
            Arg.Any<BankCategoryMapping>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSettingSameValue_ShouldStillUpdateWithNewTimestamp()
    {
        // Arrange: Setting IsExcluded to same value it already has
        // Business Scenario: Idempotent operation - should still update UpdatedAt timestamp
        var mappingId = 4;
        var existingMapping = CreateMapping(
            id: mappingId,
            bankCategory: "Online Shopping",
            categoryId: 200,
            isExcluded: true);
        var originalUpdatedAt = existingMapping.UpdatedAt;

        _mappingRepository.GetByIdAsync(mappingId, _userId, Arg.Any<CancellationToken>())
            .Returns(existingMapping);

        var command = new SetBankCategoryExclusionCommand
        {
            MappingId = mappingId,
            UserId = _userId,
            IsExcluded = true // Same value as current
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Operation completes successfully
        result.Should().NotBeNull();
        result!.IsExcluded.Should().BeTrue();

        // Verify UpdateAsync was still called (for timestamp update)
        await _mappingRepository.Received(1).UpdateAsync(
            Arg.Is<BankCategoryMapping>(m => m.UpdatedAt > originalUpdatedAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDtoWithAllMappingProperties()
    {
        // Arrange: Verify DTO contains all relevant mapping information
        var mappingId = 5;
        var bankCategory = "Supermarket and groceries";
        var categoryId = 300;
        var categoryName = "Groceries";
        var existingMapping = CreateMappingWithFullDetails(
            id: mappingId,
            bankCategory: bankCategory,
            categoryId: categoryId,
            categoryName: categoryName,
            confidenceScore: 0.92m,
            source: "AI",
            applicationCount: 15,
            overrideCount: 2,
            isActive: true,
            isExcluded: false);

        _mappingRepository.GetByIdAsync(mappingId, _userId, Arg.Any<CancellationToken>())
            .Returns(existingMapping);

        var command = new SetBankCategoryExclusionCommand
        {
            MappingId = mappingId,
            UserId = _userId,
            IsExcluded = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: All DTO properties are correctly populated
        result.Should().NotBeNull();
        result!.Id.Should().Be(mappingId);
        result.BankCategoryName.Should().Be(bankCategory);
        result.ProviderId.Should().Be("akahu");
        result.CategoryId.Should().Be(categoryId);
        result.CategoryName.Should().Be(categoryName);
        result.ConfidenceScore.Should().Be(0.92m);
        result.Source.Should().Be("AI");
        result.ApplicationCount.Should().Be(15);
        result.OverrideCount.Should().Be(2);
        result.IsActive.Should().BeTrue();
        result.IsExcluded.Should().BeTrue(); // Updated value
    }

    private BankCategoryMapping CreateMapping(int id, string bankCategory, int categoryId, bool isExcluded)
    {
        return new BankCategoryMapping
        {
            Id = id,
            BankCategoryName = bankCategory,
            NormalizedName = bankCategory.ToLowerInvariant(),
            ProviderId = "akahu",
            UserId = _userId,
            CategoryId = categoryId,
            ConfidenceScore = 0.90m,
            Source = "AI",
            ApplicationCount = 0,
            OverrideCount = 0,
            IsActive = true,
            IsExcluded = isExcluded,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Category = new Category
            {
                Id = categoryId,
                Name = "Test Category",
                Type = CategoryType.Expense,
                UserId = _userId
            }
        };
    }

    private BankCategoryMapping CreateMappingWithFullDetails(
        int id,
        string bankCategory,
        int categoryId,
        string categoryName,
        decimal confidenceScore,
        string source,
        int applicationCount,
        int overrideCount,
        bool isActive,
        bool isExcluded)
    {
        return new BankCategoryMapping
        {
            Id = id,
            BankCategoryName = bankCategory,
            NormalizedName = bankCategory.ToLowerInvariant(),
            ProviderId = "akahu",
            UserId = _userId,
            CategoryId = categoryId,
            ConfidenceScore = confidenceScore,
            Source = source,
            ApplicationCount = applicationCount,
            OverrideCount = overrideCount,
            IsActive = isActive,
            IsExcluded = isExcluded,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Category = new Category
            {
                Id = categoryId,
                Name = categoryName,
                Type = CategoryType.Expense,
                UserId = _userId
            }
        };
    }
}
