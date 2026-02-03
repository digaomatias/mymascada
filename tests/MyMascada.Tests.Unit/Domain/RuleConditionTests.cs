using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Domain;

public class RuleConditionTests
{
    [Theory]
    [InlineData(RuleConditionField.Description, RuleConditionOperator.Contains, "WALMART", "WALMART STORE", true, true)]
    [InlineData(RuleConditionField.Description, RuleConditionOperator.Contains, "walmart", "WALMART STORE", false, true)]
    [InlineData(RuleConditionField.Description, RuleConditionOperator.Contains, "walmart", "WALMART STORE", true, false)]
    [InlineData(RuleConditionField.Description, RuleConditionOperator.Equals, "WALMART", "WALMART", true, true)]
    [InlineData(RuleConditionField.Description, RuleConditionOperator.StartsWith, "WAL", "WALMART STORE", true, true)]
    [InlineData(RuleConditionField.Description, RuleConditionOperator.EndsWith, "STORE", "WALMART STORE", true, true)]
    public void Evaluate_DescriptionField_ReturnsExpected(RuleConditionField field, RuleConditionOperator op, string value, string description, bool caseSensitive, bool expected)
    {
        // Arrange
        var condition = CreateCondition(field, op, value, caseSensitive);
        var transaction = CreateTransaction(description: description);

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(RuleConditionOperator.GreaterThan, "100", 150.0, true)]
    [InlineData(RuleConditionOperator.GreaterThan, "100", 50.0, false)]
    [InlineData(RuleConditionOperator.LessThan, "100", 50.0, true)]
    [InlineData(RuleConditionOperator.LessThan, "100", 150.0, false)]
    [InlineData(RuleConditionOperator.GreaterThanOrEqual, "100", 100.0, true)]
    [InlineData(RuleConditionOperator.LessThanOrEqual, "100", 100.0, true)]
    public void Evaluate_AmountField_NumericOperators_ReturnsExpected(RuleConditionOperator op, string value, decimal amount, bool expected)
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Amount, op, value);
        var transaction = CreateTransaction(amount: amount);

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Checking", AccountType.Checking, true)]
    [InlineData("Savings", AccountType.Savings, true)]
    [InlineData("CreditCard", AccountType.CreditCard, true)]
    [InlineData("checking", AccountType.Checking, true)] // Case insensitive
    [InlineData("CHECKING", AccountType.Checking, true)]
    [InlineData("Checking", AccountType.Savings, false)]
    public void Evaluate_AccountTypeField_ReturnsExpected(string value, AccountType accountType, bool expected)
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.AccountType, RuleConditionOperator.Equals, value);
        var transaction = CreateTransaction(accountType: accountType);

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(RuleConditionField.UserDescription, "Updated description", "Updated description test", true)]
    [InlineData(RuleConditionField.UserDescription, "test", null, false)] // Null user description
    [InlineData(RuleConditionField.UserDescription, "test", "", false)] // Empty user description
    public void Evaluate_UserDescriptionField_ReturnsExpected(RuleConditionField field, string value, string? userDescription, bool expected)
    {
        // Arrange
        var condition = CreateCondition(field, RuleConditionOperator.Contains, value);
        var transaction = CreateTransaction(userDescription: userDescription);

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(RuleConditionField.ReferenceNumber, "CHK", "CHK123", true)]
    [InlineData(RuleConditionField.ReferenceNumber, "CHK", null, false)]
    [InlineData(RuleConditionField.Notes, "Store", "Main Store", true)]
    [InlineData(RuleConditionField.Notes, "Store", null, false)]
    [InlineData(RuleConditionField.Notes, "important", "This is important", true)]
    [InlineData(RuleConditionField.Notes, "important", null, false)]
    public void Evaluate_OptionalStringFields_ReturnsExpected(RuleConditionField field, string value, string? fieldValue, bool expected)
    {
        // Arrange
        var condition = CreateCondition(field, RuleConditionOperator.Contains, value);
        var transaction = CreateTransaction();
        
        // Set the appropriate field
        switch (field)
        {
            case RuleConditionField.ReferenceNumber:
                transaction.ReferenceNumber = fieldValue;
                break;
            case RuleConditionField.Notes:
                transaction.Notes = fieldValue;
                break;
        }

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(RuleConditionOperator.NotEquals, "WALMART", "TARGET", true)]
    [InlineData(RuleConditionOperator.NotEquals, "WALMART", "WALMART", false)]
    [InlineData(RuleConditionOperator.NotContains, "WALMART", "TARGET STORE", true)]
    [InlineData(RuleConditionOperator.NotContains, "WALMART", "WALMART STORE", false)]
    public void Evaluate_NegativeOperators_ReturnsExpected(RuleConditionOperator op, string value, string description, bool expected)
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Description, op, value);
        var transaction = CreateTransaction(description: description);

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_RegexOperator_ValidPattern_ReturnsExpected()
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Regex, @"ATM#\d+");
        var transaction = CreateTransaction(description: "ATM#123456 WITHDRAWAL");

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RegexOperator_InvalidPattern_ReturnsFalse()
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Regex, "[invalid");
        var transaction = CreateTransaction(description: "test");

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_RegexOperator_NoMatch_ReturnsFalse()
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Regex, @"ATM#\d+");
        var transaction = CreateTransaction(description: "WALMART STORE");

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "test", false)] // Empty field value
    [InlineData(null, "test", false)] // Null field value
    [InlineData("test", "", true)] // Empty condition value matches everything
    public void Evaluate_EdgeCases_HandledCorrectly(string? fieldValue, string conditionValue, bool expected)
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Contains, conditionValue);
        var transaction = CreateTransaction(description: fieldValue ?? "");

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_AmountField_NegativeAmount_UsesAbsoluteValue()
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, "100");
        var transaction = CreateTransaction(amount: -150.0m); // Negative amount

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().BeTrue(); // Should use absolute value (150 > 100)
    }

    [Theory]
    [InlineData("not_a_number")]
    [InlineData("")]
    [InlineData("100.50.25")]
    public void Evaluate_AmountField_InvalidNumericValue_ReturnsFalse(string invalidValue)
    {
        // Arrange
        var condition = CreateCondition(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, invalidValue);
        var transaction = CreateTransaction(amount: 150.0m);

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(RuleConditionField.Notes, "shopping", "shopping,groceries,personal", true)]
    [InlineData(RuleConditionField.Notes, "work", "shopping,groceries,personal", false)]
    [InlineData(RuleConditionField.Notes, "shop", "shopping,groceries,personal", true)] // Partial match
    public void Evaluate_NotesField_ReturnsExpected(RuleConditionField field, string value, string notes, bool expected)
    {
        // Arrange
        var condition = CreateCondition(field, RuleConditionOperator.Contains, value);
        var transaction = CreateTransaction();
        transaction.Notes = notes;

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_UnsupportedFieldType_ReturnsFalse()
    {
        // Arrange
        var condition = CreateCondition((RuleConditionField)999, RuleConditionOperator.Equals, "test");
        var transaction = CreateTransaction();

        // Act
        var result = condition.Evaluate(transaction);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_CaseSensitiveComparison_WorksCorrectly()
    {
        // Arrange
        var caseSensitiveCondition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Contains, "walmart", isCaseSensitive: true);
        var caseInsensitiveCondition = CreateCondition(RuleConditionField.Description, RuleConditionOperator.Contains, "walmart", isCaseSensitive: false);
        var transaction = CreateTransaction(description: "WALMART STORE");

        // Act & Assert
        caseSensitiveCondition.Evaluate(transaction).Should().BeFalse();
        caseInsensitiveCondition.Evaluate(transaction).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_AmountField_ExtremeValues_HandledCorrectly()
    {
        // Arrange & Act & Assert
        var maxCondition = CreateCondition(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, "1000");
        var maxTransaction = CreateTransaction(amount: decimal.MaxValue);
        maxCondition.Evaluate(maxTransaction).Should().BeTrue(); // Very large amount
        
        var minCondition = CreateCondition(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, "1000");
        var minTransaction = CreateTransaction(amount: decimal.MinValue);
        minCondition.Evaluate(minTransaction).Should().BeTrue(); // Very small amount (uses absolute)
        
        var zeroCondition = CreateCondition(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, "0");
        var zeroTransaction = CreateTransaction(amount: 0);
        zeroCondition.Evaluate(zeroTransaction).Should().BeFalse(); // Zero amount, greater than zero
        
        var zeroCondition2 = CreateCondition(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, "-1");
        zeroCondition2.Evaluate(zeroTransaction).Should().BeTrue(); // Zero amount, greater than -1
    }

    // Helper methods
    private static RuleCondition CreateCondition(
        RuleConditionField field,
        RuleConditionOperator op,
        string value,
        bool isCaseSensitive = false)
    {
        return new RuleCondition
        {
            Id = 1,
            Field = field,
            Operator = op,
            Value = value,
            IsCaseSensitive = isCaseSensitive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Transaction CreateTransaction(
        string description = "Test Transaction",
        decimal amount = 100.0m,
        AccountType accountType = AccountType.Checking,
        string? userDescription = null)
    {
        var account = new Account
        {
            Id = 1,
            Name = "Test Account",
            Type = accountType,
            UserId = Guid.NewGuid()
        };

        return new Transaction
        {
            Id = 1,
            Description = description,
            UserDescription = userDescription,
            Amount = amount,
            Account = account,
            AccountId = account.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}