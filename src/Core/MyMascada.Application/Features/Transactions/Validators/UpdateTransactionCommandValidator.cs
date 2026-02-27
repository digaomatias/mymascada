using FluentValidation;
using MyMascada.Application.Features.Transactions.Commands;

namespace MyMascada.Application.Features.Transactions.Validators;

public class UpdateTransactionCommandValidator : TransactionCommandValidatorBase<UpdateTransactionCommand>
{
    public UpdateTransactionCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("A valid transaction ID must be specified.");
    }
}
