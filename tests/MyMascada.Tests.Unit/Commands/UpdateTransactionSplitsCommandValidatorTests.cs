using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Validators;

namespace MyMascada.Tests.Unit.Commands;

public class UpdateTransactionSplitsCommandValidatorTests
{
    private readonly UpdateTransactionSplitsCommandValidator _validator = new();

    private static UpdateTransactionSplitsCommand CreateCommand(List<TransactionSplitInputDto>? splits, int transactionId = 1)
    {
        return new UpdateTransactionSplitsCommand
        {
            UserId = Guid.NewGuid(),
            TransactionId = transactionId,
            Splits = splits
        };
    }

    [Fact]
    public void Validate_WithValidSplits_ShouldPass()
    {
        var command = CreateCommand(new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -60.50m, Description = "Part A" },
            new() { CategoryId = 2, Amount = -39.50m }
        });

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullSplits_ShouldPass()
    {
        var command = CreateCommand(null);

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidTransactionId_ShouldFail()
    {
        var command = CreateCommand(null, transactionId: 0);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("transaction ID"));
    }

    [Fact]
    public void Validate_WithZeroSplitAmount_ShouldFail()
    {
        var command = CreateCommand(new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = 0m }
        });

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("must not be zero"));
    }

    [Fact]
    public void Validate_WithMoreThanTwoDecimalPlaces_ShouldFail()
    {
        var command = CreateCommand(new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -33.333m }
        });

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("2 decimal places"));
    }

    [Fact]
    public void Validate_WithInvalidCategoryId_ShouldFail()
    {
        var command = CreateCommand(new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 0, Amount = -10m }
        });

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid category ID"));
    }

    [Fact]
    public void Validate_WithTooLongDescription_ShouldFail()
    {
        var command = CreateCommand(new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -10m, Description = new string('x', 501) }
        });

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("500 characters"));
    }
}
