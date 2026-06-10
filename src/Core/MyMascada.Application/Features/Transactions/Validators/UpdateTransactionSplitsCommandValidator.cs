using FluentValidation;
using MyMascada.Application.Features.Transactions.Commands;

namespace MyMascada.Application.Features.Transactions.Validators;

public class UpdateTransactionSplitsCommandValidator : AbstractValidator<UpdateTransactionSplitsCommand>
{
    public UpdateTransactionSplitsCommandValidator()
    {
        RuleFor(x => x.TransactionId)
            .GreaterThan(0)
            .WithMessage("A valid transaction ID must be specified.");

        RuleForEach(x => x.Splits)
            .ChildRules(split =>
            {
                split.RuleFor(s => s.CategoryId)
                    .GreaterThan(0)
                    .WithMessage("Each split must specify a valid category ID.");

                split.RuleFor(s => s.Amount)
                    .NotEqual(0)
                    .WithMessage("Split amounts must not be zero.")
                    .Must(amount => amount == decimal.Round(amount, 2))
                    .WithMessage("Split amounts cannot have more than 2 decimal places.");

                split.RuleFor(s => s.Description)
                    .MaximumLength(500)
                    .WithMessage("Split description cannot exceed 500 characters.")
                    .When(s => s.Description != null);
            })
            .When(x => x.Splits != null);
    }
}
