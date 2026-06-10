using MyMascada.Application.Features.RecurringTransactions.Commands;
using MyMascada.Application.Features.RecurringTransactions.Validators;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Features.RecurringTransactions.Validators;

public class RecurringTransactionCommandValidatorTests
{
    private readonly CreateRecurringTransactionCommandValidator _createValidator = new();
    private readonly UpdateRecurringTransactionCommandValidator _updateValidator = new();

    private static CreateRecurringTransactionCommand ValidCommand()
    {
        return new CreateRecurringTransactionCommand
        {
            UserId = Guid.NewGuid(),
            AccountId = 1,
            CategoryId = 2,
            Description = "Rent",
            Amount = 1200m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2027, 7, 1),
            AutoCreate = false,
            Notes = "Apartment rent"
        };
    }

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _createValidator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_NonPositiveAmount_Fails(decimal amount)
    {
        var command = ValidCommand();
        command.Amount = amount;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Amount));
    }

    [Fact]
    public void Validate_InvalidAccountId_Fails()
    {
        var command = ValidCommand();
        command.AccountId = 0;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.AccountId));
    }

    [Fact]
    public void Validate_EmptyDescription_Fails()
    {
        var command = ValidCommand();
        command.Description = "";

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Description));
    }

    [Fact]
    public void Validate_DescriptionTooLong_Fails()
    {
        var command = ValidCommand();
        command.Description = new string('x', 201);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_Fails()
    {
        var command = ValidCommand();
        command.StartDate = new DateTime(2026, 7, 1);
        command.EndDate = new DateTime(2026, 6, 30);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.EndDate));
    }

    [Fact]
    public void Validate_EndDateEqualToStartDate_Passes()
    {
        var command = ValidCommand();
        command.StartDate = new DateTime(2026, 7, 1);
        command.EndDate = new DateTime(2026, 7, 1);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoEndDate_Passes()
    {
        var command = ValidCommand();
        command.EndDate = null;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CustomFrequencyWithoutInterval_Fails()
    {
        var command = ValidCommand();
        command.Frequency = RecurrenceFrequency.Custom;
        command.CustomIntervalDays = null;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.CustomIntervalDays));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(367)]
    public void Validate_CustomIntervalOutOfRange_Fails(int intervalDays)
    {
        var command = ValidCommand();
        command.Frequency = RecurrenceFrequency.Custom;
        command.CustomIntervalDays = intervalDays;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.CustomIntervalDays));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(366)]
    public void Validate_CustomIntervalInRange_Passes(int intervalDays)
    {
        var command = ValidCommand();
        command.Frequency = RecurrenceFrequency.Custom;
        command.CustomIntervalDays = intervalDays;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonCustomFrequency_IgnoresIntervalDays()
    {
        var command = ValidCommand();
        command.Frequency = RecurrenceFrequency.Monthly;
        command.CustomIntervalDays = null;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidFrequency_Fails()
    {
        var command = ValidCommand();
        command.Frequency = (RecurrenceFrequency)99;

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Frequency));
    }

    [Fact]
    public void Validate_NotesTooLong_Fails()
    {
        var command = ValidCommand();
        command.Notes = new string('x', 501);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Notes));
    }

    [Fact]
    public void ValidateUpdate_InvalidId_Fails()
    {
        var command = new UpdateRecurringTransactionCommand
        {
            Id = 0,
            UserId = Guid.NewGuid(),
            AccountId = 1,
            Description = "Rent",
            Amount = 1200m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2026, 7, 1)
        };

        var result = _updateValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Id));
    }

    [Fact]
    public void ValidateUpdate_ValidCommand_Passes()
    {
        var command = new UpdateRecurringTransactionCommand
        {
            Id = 5,
            UserId = Guid.NewGuid(),
            AccountId = 1,
            Description = "Rent",
            Amount = 1200m,
            Frequency = RecurrenceFrequency.Weekly,
            StartDate = new DateTime(2026, 7, 1)
        };

        var result = _updateValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
