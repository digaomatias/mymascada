using FluentValidation;
using MyMascada.Application.Features.AccountSharing.Commands;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Validators;

public class UpdateAccountShareRoleCommandValidator : AbstractValidator<UpdateAccountShareRoleCommand>
{
    public UpdateAccountShareRoleCommandValidator()
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

        RuleFor(x => x.NewRole)
            .IsInEnum()
            .WithMessage("Role must be a valid AccountShareRole")
            .Must(role => role == AccountShareRole.Viewer || role == AccountShareRole.Manager)
            .WithMessage("Role must be either Viewer or Manager");
    }
}
