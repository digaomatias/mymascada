using FluentValidation;
using MyMascada.Application.Features.AccountSharing.Commands;

namespace MyMascada.Application.Features.AccountSharing.Validators;

public class DeclineAccountShareByIdCommandValidator : AbstractValidator<DeclineAccountShareByIdCommand>
{
    public DeclineAccountShareByIdCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.ShareId)
            .GreaterThan(0)
            .WithMessage("Share ID must be greater than 0");
    }
}
