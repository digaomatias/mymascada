using FluentValidation;
using MyMascada.Application.Features.Transfers.Commands;

namespace MyMascada.Application.Features.Transfers.Validators;

/// <summary>
/// Validator for CreateTransferCommand
/// </summary>
public class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator()
    {
        RuleFor(x => x.SourceAccountId)
            .GreaterThan(0)
            .WithMessage("Source account ID must be greater than 0");

        RuleFor(x => x.DestinationAccountId)
            .GreaterThan(0)
            .WithMessage("Destination account ID must be greater than 0");

        RuleFor(x => x.SourceAccountId)
            .NotEqual(x => x.DestinationAccountId)
            .WithMessage("Source and destination accounts cannot be the same");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Transfer amount must be greater than 0")
            .LessThanOrEqualTo(999999999.99m)
            .WithMessage("Transfer amount cannot exceed 999,999,999.99");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be a 3-character code (e.g., USD)")
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be uppercase letters only");

        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0)
            .When(x => x.ExchangeRate.HasValue)
            .WithMessage("Exchange rate must be greater than 0 when specified");

        RuleFor(x => x.FeeAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.FeeAmount.HasValue)
            .WithMessage("Fee amount cannot be negative");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes cannot exceed 1000 characters");

        RuleFor(x => x.TransferDate)
            .NotEmpty()
            .WithMessage("Transfer date is required")
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1))
            .WithMessage("Transfer date cannot be more than 1 day in the future");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}