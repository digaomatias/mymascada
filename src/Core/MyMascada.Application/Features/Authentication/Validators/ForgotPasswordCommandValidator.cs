using FluentValidation;
using MyMascada.Application.Features.Authentication.Commands;

namespace MyMascada.Application.Features.Authentication.Validators;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Please provide a valid email address")
            .MaximumLength(254)
            .WithMessage("Email address is too long");
    }
}
