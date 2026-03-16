using FluentValidation;
using MyMascada.Application.Features.Wallets.Commands;

namespace MyMascada.Application.Features.Wallets.Validators;

public class CreateWalletAllocationCommandValidator : AbstractValidator<CreateWalletAllocationCommand>
{
    public CreateWalletAllocationCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .GreaterThan(0)
            .WithMessage("Wallet ID must be greater than 0");

        RuleFor(x => x.TransactionId)
            .GreaterThan(0)
            .WithMessage("Transaction ID must be greater than 0");

        RuleFor(x => x.Amount)
            .NotEqual(0)
            .WithMessage("Allocation amount cannot be zero");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note != null)
            .WithMessage("Note cannot exceed 500 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
