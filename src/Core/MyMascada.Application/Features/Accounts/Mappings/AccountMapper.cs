using Riok.Mapperly.Abstractions;
using MyMascada.Application.Features.Accounts.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.Mappings;

[Mapper]
public static partial class AccountMapper
{
    // Account -> AccountDto (with custom TypeDisplayName)
    public static AccountDto ToDto(Account account)
    {
        var dto = AccountToDtoGenerated(account);
        dto.TypeDisplayName = GetAccountTypeDisplayName(account.Type);
        return dto;
    }

    [MapperIgnoreSource(nameof(Account.UserId))]
    [MapperIgnoreSource(nameof(Account.Transactions))]
    [MapperIgnoreSource(nameof(Account.BankConnection))]
    [MapperIgnoreSource(nameof(Account.Shares))]
    [MapperIgnoreSource(nameof(Account.IsDeleted))]
    [MapperIgnoreSource(nameof(Account.DeletedAt))]
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.LastReconciledDate))]
    [MapperIgnoreSource(nameof(Account.LastReconciledBalance))]
    [MapperIgnoreTarget(nameof(AccountDto.TypeDisplayName))]
    [MapperIgnoreTarget(nameof(AccountDto.IsOwner))]
    [MapperIgnoreTarget(nameof(AccountDto.IsSharedWithMe))]
    [MapperIgnoreTarget(nameof(AccountDto.ShareRole))]
    [MapperIgnoreTarget(nameof(AccountDto.SharedByUserName))]
    private static partial AccountDto AccountToDtoGenerated(Account account);

    // CreateAccountDto -> Account
    [MapProperty(nameof(CreateAccountDto.InitialBalance), nameof(Account.CurrentBalance))]
    [MapperIgnoreTarget(nameof(Account.Id))]
    [MapperIgnoreTarget(nameof(Account.UserId))]
    [MapperIgnoreTarget(nameof(Account.Transactions))]
    [MapperIgnoreTarget(nameof(Account.BankConnection))]
    [MapperIgnoreTarget(nameof(Account.Shares))]
    [MapperIgnoreTarget(nameof(Account.CreatedAt))]
    [MapperIgnoreTarget(nameof(Account.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Account.IsDeleted))]
    [MapperIgnoreTarget(nameof(Account.CreatedBy))]
    [MapperIgnoreTarget(nameof(Account.UpdatedBy))]
    [MapperIgnoreTarget(nameof(Account.LastReconciledDate))]
    [MapperIgnoreTarget(nameof(Account.LastReconciledBalance))]
    public static partial Account ToEntity(CreateAccountDto dto);

    // UpdateAccountDto -> Account (in-place update)
    [MapperIgnoreTarget(nameof(Account.UserId))]
    [MapperIgnoreTarget(nameof(Account.CurrentBalance))]
    [MapperIgnoreTarget(nameof(Account.Transactions))]
    [MapperIgnoreTarget(nameof(Account.BankConnection))]
    [MapperIgnoreTarget(nameof(Account.Shares))]
    [MapperIgnoreTarget(nameof(Account.CreatedAt))]
    [MapperIgnoreTarget(nameof(Account.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Account.IsDeleted))]
    [MapperIgnoreTarget(nameof(Account.CreatedBy))]
    [MapperIgnoreTarget(nameof(Account.UpdatedBy))]
    [MapperIgnoreTarget(nameof(Account.LastReconciledDate))]
    [MapperIgnoreTarget(nameof(Account.LastReconciledBalance))]
    public static partial void ApplyTo(UpdateAccountDto dto, Account account);

    // Account -> AccountWithBalanceDto
    [MapProperty(nameof(Account.Type), nameof(AccountWithBalanceDto.TypeDisplayName), Use = nameof(GetAccountTypeDisplayName))]
    [MapperIgnoreTarget(nameof(AccountWithBalanceDto.CalculatedBalance))]
    [MapperIgnoreTarget(nameof(AccountWithBalanceDto.IsOwner))]
    [MapperIgnoreTarget(nameof(AccountWithBalanceDto.IsSharedWithMe))]
    [MapperIgnoreTarget(nameof(AccountWithBalanceDto.ShareRole))]
    [MapperIgnoreTarget(nameof(AccountWithBalanceDto.SharedByUserName))]
    [MapperIgnoreSource(nameof(Account.UserId))]
    [MapperIgnoreSource(nameof(Account.Transactions))]
    [MapperIgnoreSource(nameof(Account.BankConnection))]
    [MapperIgnoreSource(nameof(Account.Shares))]
    [MapperIgnoreSource(nameof(Account.IsDeleted))]
    [MapperIgnoreSource(nameof(Account.LastReconciledDate))]
    [MapperIgnoreSource(nameof(Account.LastReconciledBalance))]
    [MapperIgnoreSource(nameof(Account.LastFourDigits))]
    public static partial AccountWithBalanceDto ToWithBalanceDto(Account account);

    // Account -> AccountDetailsDto (with custom TypeDisplayName)
    public static AccountDetailsDto ToDetailsDto(Account account)
    {
        var dto = AccountToDetailsDtoGenerated(account);
        dto.TypeDisplayName = GetAccountTypeDisplayName(account.Type);
        return dto;
    }

    [MapperIgnoreTarget(nameof(AccountDetailsDto.TypeDisplayName))]
    [MapperIgnoreTarget(nameof(AccountDetailsDto.CalculatedBalance))]
    [MapperIgnoreTarget(nameof(AccountDetailsDto.MonthlySpending))]
    private static partial AccountDetailsDto AccountToDetailsDtoGenerated(Account account);

    private static string GetAccountTypeDisplayName(AccountType type)
    {
        return type switch
        {
            AccountType.Checking => "Checking Account",
            AccountType.Savings => "Savings Account",
            AccountType.CreditCard => "Credit Card",
            AccountType.Investment => "Investment Account",
            AccountType.Loan => "Loan Account",
            AccountType.Cash => "Cash Account",
            AccountType.Other => "Other Account",
            _ => type.ToString()
        };
    }
}
