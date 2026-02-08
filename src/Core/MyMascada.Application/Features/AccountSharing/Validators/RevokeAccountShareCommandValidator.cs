using FluentValidation;
using MyMascada.Application.Features.AccountSharing.Commands;

namespace MyMascada.Application.Features.AccountSharing.Validators;

public class RevokeAccountShareCommandValidator : AbstractValidator<RevokeAccountShareCommand>
{
    public RevokeAccountShareCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .WithMessage("Account ID must be greater than 0");

        RuleFor(x => x.ShareId)
            .GreaterThan(0)
            .WithMessage("Share ID must be greater than 0");
    }
}
