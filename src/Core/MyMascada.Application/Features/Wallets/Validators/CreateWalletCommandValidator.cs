using FluentValidation;
using MyMascada.Application.Features.Wallets.Commands;

namespace MyMascada.Application.Features.Wallets.Validators;

public class CreateWalletCommandValidator : AbstractValidator<CreateWalletCommand>
{
    public CreateWalletCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Wallet name is required")
            .MaximumLength(100)
            .WithMessage("Wallet name cannot exceed 100 characters");

        RuleFor(x => x.Color)
            .Matches(@"^#[0-9A-Fa-f]{6}$")
            .When(x => !string.IsNullOrEmpty(x.Color))
            .WithMessage("Color must be a valid hex color (e.g., #FF5733)");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be a 3-character code (e.g., NZD)")
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be letters only");

        RuleFor(x => x.Icon)
            .MaximumLength(50)
            .When(x => x.Icon != null)
            .WithMessage("Icon cannot exceed 50 characters");

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0)
            .When(x => x.TargetAmount.HasValue)
            .WithMessage("Target amount must be greater than 0 when specified");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
