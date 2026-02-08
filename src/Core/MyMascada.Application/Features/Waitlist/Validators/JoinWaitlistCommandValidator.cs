using FluentValidation;
using MyMascada.Application.Features.Waitlist.Commands;

namespace MyMascada.Application.Features.Waitlist.Validators;

public class JoinWaitlistCommandValidator : AbstractValidator<JoinWaitlistCommand>
{
    public JoinWaitlistCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Please provide a valid email address")
            .MaximumLength(254).WithMessage("Email address is too long");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name is too long");
    }
}
