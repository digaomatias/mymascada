using FluentValidation;
using MyMascada.Application.Features.AccountSharing.Commands;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Validators;

public class CreateAccountShareCommandValidator : AbstractValidator<CreateAccountShareCommand>
{
    public CreateAccountShareCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .WithMessage("Account ID must be greater than 0");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Please provide a valid email address")
            .MaximumLength(254)
            .WithMessage("Email address is too long");

        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be a valid AccountShareRole")
            .Must(role => role == AccountShareRole.Viewer || role == AccountShareRole.Manager)
            .WithMessage("Role must be either Viewer or Manager");
    }
}
