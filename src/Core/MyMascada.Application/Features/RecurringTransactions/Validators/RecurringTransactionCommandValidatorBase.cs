using FluentValidation;
using MyMascada.Application.Features.RecurringTransactions.Commands;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.Validators;

/// <summary>
/// Base validator containing shared validation rules for recurring transaction commands.
/// </summary>
public abstract class RecurringTransactionCommandValidatorBase<TCommand> : AbstractValidator<TCommand>
    where TCommand : IRecurringTransactionBaseCommand
{
    protected RecurringTransactionCommandValidatorBase()
    {
        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .WithMessage("A valid account must be specified.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(200)
            .WithMessage("Description cannot exceed 200 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000m)
            .WithMessage("Amount must not exceed 1,000,000.");

        RuleFor(x => x.Frequency)
            .IsInEnum()
            .WithMessage("A valid frequency must be specified.");

        RuleFor(x => x.CustomIntervalDays)
            .NotNull()
            .WithMessage("Custom interval days is required when frequency is Custom.")
            .InclusiveBetween(1, 366)
            .WithMessage("Custom interval days must be between 1 and 366.")
            .When(x => x.Frequency == RecurrenceFrequency.Custom);

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after the start date.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes != null);
    }
}

public class CreateRecurringTransactionCommandValidator
    : RecurringTransactionCommandValidatorBase<CreateRecurringTransactionCommand>
{
}

public class UpdateRecurringTransactionCommandValidator
    : RecurringTransactionCommandValidatorBase<UpdateRecurringTransactionCommand>
{
    public UpdateRecurringTransactionCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("A valid recurring transaction ID must be specified.");
    }
}
