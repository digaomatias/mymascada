using AutoMapper;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System;
using System.Linq;

namespace MyMascada.Application.Features.Transactions.Mappings;

public class TransactionMappingProfile : Profile
{
    public TransactionMappingProfile()
    {
        // Transaction -> TransactionDto (for lists)
        CreateMap<Transaction, TransactionDto>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.Source, opt => opt.MapFrom(src => src.Source))
            .ForMember(dest => dest.AccountName, opt => opt.MapFrom(src => 
                src.Account != null ? src.Account.Name : string.Empty))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Name : null))
            .ForMember(dest => dest.CategoryColor, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Color : null));

        // Transaction -> TransactionDetailDto (for single view)
        CreateMap<Transaction, TransactionDetailDto>()
            .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => 
                src.Amount >= 0 ? "Income" : "Expense"))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => 
                src.Status.ToString()))
            .ForMember(dest => dest.Source, opt => opt.MapFrom(src => 
                src.Source.ToString()))
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => 
                string.IsNullOrEmpty(src.Tags) 
                    ? new List<string>() 
                    : src.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()))
            .ForMember(dest => dest.AccountName, opt => opt.MapFrom(src => 
                src.Account != null ? src.Account.Name : string.Empty))
            .ForMember(dest => dest.AccountType, opt => opt.MapFrom(src => 
                src.Account != null ? src.Account.Type.ToString() : string.Empty))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => 
                src.Account != null ? src.Account.Currency : string.Empty))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Name : null))
            .ForMember(dest => dest.CategoryColor, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Color : null))
            .ForMember(dest => dest.CategoryIcon, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Icon : null))
            .ForMember(dest => dest.RelatedAccountName, opt => opt.MapFrom(src => 
                src.RelatedTransaction != null && src.RelatedTransaction.Account != null 
                    ? src.RelatedTransaction.Account.Name 
                    : null))
            .ForMember(dest => dest.Splits, opt => opt.MapFrom(src => 
                src.Splits != null && src.Splits.Any() ? src.Splits : null));

        // TransactionSplit -> TransactionSplitDto
        CreateMap<TransactionSplit, TransactionSplitDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Name : string.Empty))
            .ForMember(dest => dest.CategoryColor, opt => opt.MapFrom(src => 
                src.Category != null ? src.Category.Color : null));

        // CreateTransactionDto -> Transaction
        CreateMap<CreateTransactionDto, Transaction>()
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => 
                src.Type.ToLower() == "income" ? Math.Abs(src.Amount) : -Math.Abs(src.Amount)))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => 
                Enum.Parse<TransactionStatus>(src.Status, true)))
            .ForMember(dest => dest.Source, opt => opt.MapFrom(src => 
                TransactionSource.Manual))
            .ForMember(dest => dest.IsReviewed, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Account, opt => opt.Ignore())
            .ForMember(dest => dest.Category, opt => opt.Ignore())
            .ForMember(dest => dest.RelatedTransaction, opt => opt.Ignore())
            .ForMember(dest => dest.Transfer, opt => opt.Ignore())
            .ForMember(dest => dest.Splits, opt => opt.Ignore());

        // UpdateTransactionDto -> Transaction (for partial updates)
        CreateMap<UpdateTransactionDto, Transaction>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => 
                Enum.Parse<TransactionStatus>(src.Status, true)))
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => 
                src.Amount)) // Amount sign should already be correct from frontend
            .ForMember(dest => dest.Account, opt => opt.Ignore())
            .ForMember(dest => dest.Category, opt => opt.Ignore())
            .ForMember(dest => dest.RelatedTransaction, opt => opt.Ignore())
            .ForMember(dest => dest.Transfer, opt => opt.Ignore())
            .ForMember(dest => dest.Splits, opt => opt.Ignore())
            .ForMember(dest => dest.Source, opt => opt.Ignore())
            .ForMember(dest => dest.AccountId, opt => opt.Ignore())
            .ForMember(dest => dest.ExternalId, opt => opt.Ignore())
            .ForMember(dest => dest.TransferId, opt => opt.Ignore())
            .ForMember(dest => dest.RelatedTransactionId, opt => opt.Ignore())
            .ForMember(dest => dest.IsTransferSource, opt => opt.Ignore());
    }
}