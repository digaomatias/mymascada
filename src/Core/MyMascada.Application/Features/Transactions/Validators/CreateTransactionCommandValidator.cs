using FluentValidation;
using MyMascada.Application.Features.Transactions.Commands;

namespace MyMascada.Application.Features.Transactions.Validators;

public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .NotEqual(0)
            .WithMessage("Amount must not be zero.")
            .InclusiveBetween(-1_000_000_000m, 1_000_000_000m)
            .WithMessage("Amount must be between -1,000,000,000 and 1,000,000,000.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.TransactionDate)
            .NotEmpty()
            .WithMessage("Transaction date is required.");

        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .WithMessage("A valid account must be specified.");

        RuleFor(x => x.UserDescription)
            .MaximumLength(500)
            .WithMessage("User description cannot exceed 500 characters.")
            .When(x => x.UserDescription != null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes != null);

        RuleFor(x => x.Location)
            .MaximumLength(250)
            .WithMessage("Location cannot exceed 250 characters.")
            .When(x => x.Location != null);
    }
}
