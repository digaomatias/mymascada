using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Domain;

public class CategorizationRuleTests
{
    [Theory]
    [InlineData("WALMART STORE", "walmart", RuleType.Contains, false, true)]
    [InlineData("WALMART STORE", "WALMART", RuleType.Contains, true, true)]
    [InlineData("WALMART STORE", "walmart", RuleType.Contains, true, false)]
    [InlineData("AMAZON.COM", "AMAZON", RuleType.StartsWith, false, true)]
    [InlineData("Store WALMART", "WALMART", RuleType.EndsWith, false, true)]
    [InlineData("WALMART", "WALMART", RuleType.Equals, false, true)]
    [InlineData("ATM#123456", @"ATM#\d+", RuleType.Regex, false, true)]
    public void MatchesDescription_ValidPatterns_ReturnsExpected(string description, string pattern, RuleType type, bool caseSensitive, bool expected)
    {
        // Arrange
        var rule = CreateRule(pattern: pattern, type: type, isCaseSensitive: caseSensitive);
        
        // Act
        var result = rule.MatchesDescription(description);
        
        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", "walmart", RuleType.Contains, false, false)]
    [InlineData(null, "walmart", RuleType.Contains, false, false)]
    [InlineData("TARGET STORE", "walmart", RuleType.Contains, false, false)]
    [InlineData("walmart store", "WALMART", RuleType.Contains, true, false)]
    [InlineData("STORE WALMART", "AMAZON", RuleType.StartsWith, false, false)]
    [InlineData("WALMART STORE", "TARGET", RuleType.EndsWith, false, false)]
    [InlineData("WALMART STORE", "WALMART", RuleType.Equals, false, false)]
    [InlineData("ATM123", @"ATM#\d+", RuleType.Regex, false, false)]
    public void MatchesDescription_InvalidPatterns_ReturnsFalse(string description, string pattern, RuleType type, bool caseSensitive, bool expected)
    {
        // Arrange
        var rule = CreateRule(pattern: pattern, type: type, isCaseSensitive: caseSensitive);
        
        // Act
        var result = rule.MatchesDescription(description);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesDescription_RegexWithInvalidPattern_ReturnsFalse()
    {
        // Arrange
        var rule = CreateRule(pattern: "[invalid", type: RuleType.Regex);
        
        // Act
        var result = rule.MatchesDescription("test");
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesDescription_UnsupportedRuleType_ReturnsFalse()
    {
        // Arrange
        var rule = CreateRule(pattern: "test", type: (RuleType)999);
        
        // Act
        var result = rule.MatchesDescription("test");
        
        // Assert
        result.Should().BeFalse();
    }

    public static IEnumerable<object?[]> AmountRangeTestData =>
        new List<object?[]>
        {
            new object?[] { 100.50m, null, null, true },
            new object?[] { 100.50m, 50.00m, 200.00m, true },
            new object?[] { 100.50m, 150.00m, 200.00m, false },
            new object?[] { 100.50m, 50.00m, 75.00m, false },
            new object?[] { -100.50m, 50.00m, 200.00m, true }, // Uses absolute value
            new object?[] { 0m, 0m, 100m, true },
            new object?[] { 100m, 100m, 100m, true }
        };

    [Theory]
    [MemberData(nameof(AmountRangeTestData))]
    public void MatchesAmount_VariousAmountRanges_ReturnsExpected(decimal amount, decimal? minAmount, decimal? maxAmount, bool expected)
    {
        // Arrange
        var rule = CreateRule(minAmount: minAmount, maxAmount: maxAmount);
        
        // Act
        var result = rule.MatchesAmount(amount);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesAmount_NegativeAmountUsesAbsoluteValue()
    {
        // Arrange
        var rule = CreateRule(minAmount: 50, maxAmount: 150);
        
        // Act & Assert
        rule.MatchesAmount(-100).Should().BeTrue(); // Should use absolute value 100
    }

    [Theory]
    [InlineData(null, AccountType.Checking, true)] // No restriction
    [InlineData("", AccountType.Checking, true)] // Empty restriction
    [InlineData("Checking", AccountType.Checking, true)]
    [InlineData("Checking,Savings", AccountType.Checking, true)]
    [InlineData("Checking,Savings", AccountType.CreditCard, false)]
    [InlineData("checking", AccountType.Checking, true)] // Case insensitive
    [InlineData("CHECKING", AccountType.Checking, true)]
    public void MatchesAccountType_VariousAccountTypes_ReturnsExpected(string? accountTypes, AccountType accountType, bool expected)
    {
        // Arrange
        var rule = CreateRule(accountTypes: accountTypes);
        
        // Act
        var result = rule.MatchesAccountType(accountType);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesAccountType_WhitespaceInAccountTypes_HandledCorrectly()
    {
        // Arrange
        var rule = CreateRule(accountTypes: " Checking , Savings , CreditCard ");
        
        // Act & Assert
        rule.MatchesAccountType(AccountType.Checking).Should().BeTrue();
        rule.MatchesAccountType(AccountType.Savings).Should().BeTrue();
        rule.MatchesAccountType(AccountType.CreditCard).Should().BeTrue();
        rule.MatchesAccountType(AccountType.Investment).Should().BeFalse();
    }

    [Theory]
    [InlineData(50.00, 50.00, 100.00, true)]  // Exactly at min
    [InlineData(100.00, 50.00, 100.00, true)] // Exactly at max
    [InlineData(49.99, 50.00, 100.00, false)] // Just below min
    [InlineData(100.01, 50.00, 100.00, false)] // Just above max
    [InlineData(75.00, 50.00, 100.00, true)]   // Within range
    public void MatchesAmount_BoundaryValues_ReturnsExpected(decimal amount, decimal min, decimal max, bool expected)
    {
        // Arrange
        var rule = CreateRule(minAmount: min, maxAmount: max);
        
        // Act
        var result = rule.MatchesAmount(amount);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesAmount_MinAmountOnly_WorksCorrectly()
    {
        // Arrange
        var rule = CreateRule(minAmount: 50.00m, maxAmount: null);
        
        // Act & Assert
        rule.MatchesAmount(50.00m).Should().BeTrue();
        rule.MatchesAmount(1000.00m).Should().BeTrue();
        rule.MatchesAmount(49.99m).Should().BeFalse();
    }

    [Fact]
    public void MatchesAmount_MaxAmountOnly_WorksCorrectly()
    {
        // Arrange
        var rule = CreateRule(minAmount: null, maxAmount: 100.00m);
        
        // Act & Assert
        rule.MatchesAmount(100.00m).Should().BeTrue();
        rule.MatchesAmount(0.01m).Should().BeTrue();
        rule.MatchesAmount(100.01m).Should().BeFalse();
    }

    [Theory]
    [InlineData(10, 2, 0.833)] // 10 matches, 2 corrections = 83.3% accuracy
    [InlineData(5, 0, 1.0)]    // 5 matches, 0 corrections = 100% accuracy
    [InlineData(0, 0, 1.0)]    // No data = assume perfect
    [InlineData(3, 3, 0.5)]    // 3 matches, 3 corrections = 50% accuracy
    public void GetAccuracyRate_VariousScenarios_ReturnsExpected(int matchCount, int correctionCount, double expected)
    {
        // Arrange
        var rule = CreateRule();
        rule.MatchCount = matchCount;
        rule.CorrectionCount = correctionCount;
        
        // Act
        var accuracy = rule.GetAccuracyRate();
        
        // Assert
        accuracy.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void GetAccuracyRate_HighCorrectionCount_ReturnsLowAccuracy()
    {
        // Arrange
        var rule = CreateRule();
        rule.MatchCount = 1;
        rule.CorrectionCount = 9; // 1 success out of 10 total = 10% accuracy
        
        // Act
        var accuracy = rule.GetAccuracyRate();
        
        // Assert
        accuracy.Should().BeApproximately(0.1, 0.001);
    }

    [Fact]
    public void RecordSuccessfulMatch_IncrementsMatchCount()
    {
        // Arrange
        var rule = CreateRule();
        var initialCount = rule.MatchCount;
        var initialUpdateTime = rule.UpdatedAt;

        // Act
        rule.RecordSuccessfulMatch();

        // Assert
        rule.MatchCount.Should().Be(initialCount + 1);
        rule.UpdatedAt.Should().BeOnOrAfter(initialUpdateTime);
    }

    [Fact]
    public void RecordCorrection_IncrementsCorrectionCount()
    {
        // Arrange
        var rule = CreateRule();
        var initialCount = rule.CorrectionCount;
        var initialUpdateTime = rule.UpdatedAt;

        // Act
        rule.RecordCorrection();

        // Assert
        rule.CorrectionCount.Should().Be(initialCount + 1);
        rule.UpdatedAt.Should().BeOnOrAfter(initialUpdateTime);
    }

    [Fact]
    public void RecordApplication_CreatesApplicationAndUpdatesStats()
    {
        // Arrange
        var rule = CreateRule();
        var initialMatchCount = rule.MatchCount;
        var initialApplicationCount = rule.Applications.Count;
        
        // Act
        var application = rule.RecordApplication(
            transactionId: 123,
            categoryId: 456,
            confidenceScore: 0.9m,
            triggerSource: "Manual"
        );
        
        // Assert
        application.Should().NotBeNull();
        application.RuleId.Should().Be(rule.Id);
        application.TransactionId.Should().Be(123);
        application.CategoryId.Should().Be(456);
        application.ConfidenceScore.Should().Be(0.9m);
        application.TriggerSource.Should().Be("Manual");
        rule.MatchCount.Should().Be(initialMatchCount + 1);
        rule.Applications.Count.Should().Be(initialApplicationCount + 1);
    }

    [Fact]
    public void GetSuccessRate_BasedOnApplications_ReturnsCorrectRate()
    {
        // Arrange
        var rule = CreateRule();
        var applications = new List<RuleApplication>
        {
            CreateApplication(wasCorrected: false),
            CreateApplication(wasCorrected: false),
            CreateApplication(wasCorrected: true),
            CreateApplication(wasCorrected: false)
        };
        
        rule.Applications = applications;
        
        // Act
        var successRate = rule.GetSuccessRate();
        
        // Assert
        successRate.Should().BeApproximately(0.75, 0.001); // 3 out of 4 successful
    }

    [Fact]
    public void GetSuccessRate_NoApplications_ReturnsOne()
    {
        // Arrange
        var rule = CreateRule();
        rule.Applications = new List<RuleApplication>();
        
        // Act
        var successRate = rule.GetSuccessRate();
        
        // Assert
        successRate.Should().Be(1.0);
    }

    [Fact]
    public void HasAdvancedConditions_WithActiveConditions_ReturnsTrue()
    {
        // Arrange
        var rule = CreateRule();
        rule.Conditions.Add(CreateCondition(RuleConditionField.Description, RuleConditionOperator.Contains, "WALMART"));
        
        // Act & Assert
        rule.HasAdvancedConditions().Should().BeTrue();
    }

    [Fact]
    public void HasAdvancedConditions_WithDeletedConditions_ReturnsFalse()
    {
        // Arrange
        var rule = CreateRule();
        var condition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Contains, "WALMART");
        condition.IsDeleted = true;
        rule.Conditions.Add(condition);
        
        // Act & Assert
        rule.HasAdvancedConditions().Should().BeFalse();
    }

    [Fact]
    public void Matches_TransactionWithNullDescription_HandledGracefully()
    {
        // Arrange
        var rule = CreateRule(pattern: "test");
        var transaction = CreateTransaction();
        transaction.Description = null!;
        
        // Act
        var result = rule.Matches(transaction);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesAmount_ExtremeAmountValues_HandledCorrectly()
    {
        // Arrange
        var rule = CreateRule();
        
        // Act & Assert - Should not throw exception
        rule.MatchesAmount(decimal.MaxValue).Should().BeTrue(); // No amount restrictions
        rule.MatchesAmount(decimal.MinValue).Should().BeTrue(); // No amount restrictions  
        rule.MatchesAmount(0).Should().BeTrue(); // No amount restrictions
    }

    // Helper methods
    private static CategorizationRule CreateRule(
        int id = 1,
        string pattern = "TEST",
        RuleType type = RuleType.Contains,
        bool isCaseSensitive = false,
        int priority = 0,
        bool isActive = true,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        string? accountTypes = null,
        int categoryId = 1,
        double? confidenceScore = 0.8)
    {
        return new CategorizationRule
        {
            Id = id,
            Name = $"Test Rule {id}",
            Pattern = pattern,
            Type = type,
            IsCaseSensitive = isCaseSensitive,
            Priority = priority,
            IsActive = isActive,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            AccountTypes = accountTypes,
            CategoryId = categoryId,
            ConfidenceScore = confidenceScore,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Transaction CreateTransaction(
        int id = 1,
        string description = "Test Transaction",
        decimal amount = 100.0m,
        AccountType accountType = AccountType.Checking,
        Guid? userId = null)
    {
        var account = new Account
        {
            Id = 1,
            Name = "Test Account",
            Type = accountType,
            UserId = userId ?? Guid.NewGuid()
        };

        return new Transaction
        {
            Id = id,
            Description = description,
            Amount = amount,
            Account = account,
            AccountId = account.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static RuleCondition CreateCondition(RuleConditionField field, RuleConditionOperator op, string value)
    {
        return new RuleCondition
        {
            Field = field,
            Operator = op,
            Value = value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static RuleApplication CreateApplication(bool wasCorrected = false)
    {
        var application = new RuleApplication
        {
            Id = Random.Shared.Next(1, 1000),
            RuleId = 1,
            TransactionId = Random.Shared.Next(1, 1000),
            CategoryId = 1,
            ConfidenceScore = 0.8m,
            TriggerSource = "Test",
            WasCorrected = wasCorrected,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (wasCorrected)
        {
            application.CorrectedCategoryId = 2;
            application.CorrectedAt = DateTime.UtcNow;
        }

        return application;
    }
}