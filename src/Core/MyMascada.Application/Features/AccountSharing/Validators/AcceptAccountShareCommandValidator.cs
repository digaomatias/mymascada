using FluentValidation;
using MyMascada.Application.Features.AccountSharing.Commands;

namespace MyMascada.Application.Features.AccountSharing.Validators;

public class AcceptAccountShareCommandValidator : AbstractValidator<AcceptAccountShareCommand>
{
    public AcceptAccountShareCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Invitation token is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
