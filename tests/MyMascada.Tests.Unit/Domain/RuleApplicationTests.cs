using MyMascada.Domain.Entities;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Domain;

public class RuleApplicationTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var application = new RuleApplication();

        // Assert
        application.WasCorrected.Should().BeFalse();
        application.CorrectedCategoryId.Should().BeNull();
        application.CorrectedAt.Should().BeNull();
        application.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        application.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordCorrection_UpdatesCorrectlyOnFirstCorrection()
    {
        // Arrange
        var application = CreateApplication();
        var originalUpdatedAt = application.UpdatedAt;
        var newCategoryId = 456;

        // Act
        application.RecordCorrection(newCategoryId);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(newCategoryId);
        application.CorrectedAt.Should().NotBeNull();
        application.CorrectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        application.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void RecordCorrection_UpdatesCorrectionMultipleTimes()
    {
        // Arrange
        var application = CreateApplication();
        application.RecordCorrection(456);
        var firstCorrectionTime = application.CorrectedAt;

        // Act - Record another correction
        application.RecordCorrection(789);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(789); // Updated to latest
        application.CorrectedAt.Should().BeOnOrAfter(firstCorrectionTime!.Value);
    }

    [Fact]
    public void RecordCorrection_WithSameCategoryId_StillUpdatesTimestamp()
    {
        // Arrange
        var application = CreateApplication();
        application.CategoryId = 123;
        var originalUpdatedAt = application.UpdatedAt;

        // Act - Correct to the same category
        application.RecordCorrection(123);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(123);
        application.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void RecordCorrection_WithZeroCategoryId_HandledCorrectly()
    {
        // Arrange
        var application = CreateApplication();

        // Act
        application.RecordCorrection(0);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(0);
        application.CorrectedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordCorrection_WithNegativeCategoryId_HandledCorrectly()
    {
        // Arrange
        var application = CreateApplication();

        // Act
        application.RecordCorrection(-1);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(-1);
        application.CorrectedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsSuccessful_WhenNotCorrected_ReturnsTrue()
    {
        // Arrange
        var application = CreateApplication();
        application.WasCorrected = false;

        // Act & Assert
        application.IsSuccessful().Should().BeTrue();
    }

    [Fact]
    public void IsSuccessful_WhenCorrected_ReturnsFalse()
    {
        // Arrange
        var application = CreateApplication();
        application.RecordCorrection(456);

        // Act & Assert
        application.IsSuccessful().Should().BeFalse();
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void ConfidenceScore_VariousValues_StoredCorrectly(decimal confidence)
    {
        // Arrange & Act
        var application = CreateApplication(confidenceScore: confidence);

        // Assert
        application.ConfidenceScore.Should().Be(confidence);
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("Automatic")]
    [InlineData("AI_Suggestion")]
    [InlineData("Batch_Process")]
    public void TriggerSource_VariousValues_StoredCorrectly(string triggerSource)
    {
        // Arrange & Act
        var application = CreateApplication(triggerSource: triggerSource);

        // Assert
        application.TriggerSource.Should().Be(triggerSource);
    }

    [Fact]
    public void TriggerSource_EmptyString_StoredCorrectly()
    {
        // Arrange & Act
        var application = CreateApplication(triggerSource: "");

        // Assert
        application.TriggerSource.Should().BeEmpty();
    }

    [Fact]
    public void ApplicationLifecycle_CompleteFlow_WorksCorrectly()
    {
        // Arrange - Create initial application
        var application = CreateApplication();
        var originalCreatedAt = application.CreatedAt;

        // Act & Assert - Initial state
        application.IsSuccessful().Should().BeTrue();
        application.WasCorrected.Should().BeFalse();

        // Act - Record correction
        application.RecordCorrection(999);

        // Assert - After correction
        application.IsSuccessful().Should().BeFalse();
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(999);
        application.CreatedAt.Should().Be(originalCreatedAt); // Should not change
        application.UpdatedAt.Should().BeOnOrAfter(originalCreatedAt);
    }

    [Fact]
    public void GetCorrectionMetadata_WhenNotCorrected_ReturnsCorrectValues()
    {
        // Arrange
        var application = CreateApplication();

        // Act & Assert
        application.WasCorrected.Should().BeFalse();
        application.CorrectedCategoryId.Should().BeNull();
        application.CorrectedAt.Should().BeNull();
    }

    [Fact]
    public void GetCorrectionMetadata_WhenCorrected_ReturnsCorrectValues()
    {
        // Arrange
        var application = CreateApplication();
        var beforeCorrection = DateTime.UtcNow;

        // Act
        application.RecordCorrection(123);
        var afterCorrection = DateTime.UtcNow;

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(123);
        application.CorrectedAt.Should().BeOnOrAfter(beforeCorrection);
        application.CorrectedAt.Should().BeOnOrBefore(afterCorrection);
    }

    [Fact]
    public void ConcurrentCorrections_LastOneWins()
    {
        // Arrange
        var application = CreateApplication();

        // Act - Simulate rapid corrections
        application.RecordCorrection(100);
        Thread.Sleep(1); // Ensure different timestamps
        application.RecordCorrection(200);
        Thread.Sleep(1);
        application.RecordCorrection(300);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(300);
    }

    [Fact]
    public void ForeignKeyRelationships_SetCorrectly()
    {
        // Arrange & Act
        var application = CreateApplication(
            ruleId: 123,
            transactionId: 456,
            categoryId: 789
        );

        // Assert
        application.RuleId.Should().Be(123);
        application.TransactionId.Should().Be(456);
        application.CategoryId.Should().Be(789);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.001)]
    [InlineData(0.999)]
    public void ConfidenceScore_BoundaryValues_HandledCorrectly(decimal confidence)
    {
        // Arrange & Act
        var application = CreateApplication(confidenceScore: confidence);

        // Assert
        application.ConfidenceScore.Should().Be(confidence);
    }

    [Fact]
    public void UpdatedAt_AutomaticallySetOnCorrection()
    {
        // Arrange
        var application = CreateApplication();
        var originalUpdatedAt = application.UpdatedAt;
        Thread.Sleep(10); // Ensure time difference

        // Act
        application.RecordCorrection(123);

        // Assert
        application.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    // Helper method
    private static RuleApplication CreateApplication(
        int id = 1,
        int ruleId = 1,
        int transactionId = 1,
        int categoryId = 1,
        decimal confidenceScore = 0.8m,
        string triggerSource = "Test")
    {
        return new RuleApplication
        {
            Id = id,
            RuleId = ruleId,
            TransactionId = transactionId,
            CategoryId = categoryId,
            ConfidenceScore = confidenceScore,
            TriggerSource = triggerSource,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}