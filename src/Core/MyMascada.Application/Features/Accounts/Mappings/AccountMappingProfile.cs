using AutoMapper;
using MyMascada.Application.Features.Accounts.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.Mappings;

public class AccountMappingProfile : Profile
{
    public AccountMappingProfile()
    {
        // Account -> AccountDto
        CreateMap<Account, AccountDto>()
            .ForMember(dest => dest.TypeDisplayName, opt => opt.MapFrom(src =>
                GetAccountTypeDisplayName(src.Type)))
            .ForMember(dest => dest.IsOwner, opt => opt.Ignore())
            .ForMember(dest => dest.IsSharedWithMe, opt => opt.Ignore())
            .ForMember(dest => dest.ShareRole, opt => opt.Ignore())
            .ForMember(dest => dest.SharedByUserName, opt => opt.Ignore());

        // CreateAccountDto -> Account
        CreateMap<CreateAccountDto, Account>()
            .ForMember(dest => dest.CurrentBalance, opt => opt.MapFrom(src => src.InitialBalance))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.Transactions, opt => opt.Ignore());

        // UpdateAccountDto -> Account (for partial updates)
        CreateMap<UpdateAccountDto, Account>()
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentBalance, opt => opt.Ignore()) // Balance updated through transactions
            .ForMember(dest => dest.Transactions, opt => opt.Ignore());

        // Account -> AccountWithBalanceDto
        CreateMap<Account, AccountWithBalanceDto>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => (int)src.Type))
            .ForMember(dest => dest.CalculatedBalance, opt => opt.Ignore()) // Will be set manually in controller
            .ForMember(dest => dest.IsOwner, opt => opt.Ignore())
            .ForMember(dest => dest.IsSharedWithMe, opt => opt.Ignore())
            .ForMember(dest => dest.ShareRole, opt => opt.Ignore())
            .ForMember(dest => dest.SharedByUserName, opt => opt.Ignore());

        // Account -> AccountDetailsDto
        CreateMap<Account, AccountDetailsDto>()
            .ForMember(dest => dest.TypeDisplayName, opt => opt.MapFrom(src => 
                GetAccountTypeDisplayName(src.Type)))
            .ForMember(dest => dest.CalculatedBalance, opt => opt.Ignore()) // Will be set from transaction calculations
            .ForMember(dest => dest.MonthlySpending, opt => opt.Ignore()); // Will be set manually in query handler
    }

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
            _ => type.ToString()
        };
    }
}