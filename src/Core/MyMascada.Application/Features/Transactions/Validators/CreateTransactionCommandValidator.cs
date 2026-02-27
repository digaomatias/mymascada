using FluentValidation;
using MyMascada.Application.Features.Transactions.Commands;

namespace MyMascada.Application.Features.Transactions.Validators;

public class CreateTransactionCommandValidator : TransactionCommandValidatorBase<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .WithMessage("A valid account must be specified.");
    }
}
