using Riok.Mapperly.Abstractions;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Transactions.Mappings;

[Mapper]
public static partial class TransactionMapper
{
    // Transaction -> TransactionDto (for lists)
    public static TransactionDto ToDto(Transaction transaction)
    {
        var dto = TransactionToDtoGenerated(transaction);
        dto.AccountName = transaction.Account?.Name ?? string.Empty;
        dto.CategoryName = transaction.Category?.Name;
        dto.CategoryColor = transaction.Category?.Color;
        return dto;
    }

    [MapperIgnoreTarget(nameof(TransactionDto.AccountName))]
    [MapperIgnoreTarget(nameof(TransactionDto.CategoryName))]
    [MapperIgnoreTarget(nameof(TransactionDto.CategoryColor))]
    private static partial TransactionDto TransactionToDtoGenerated(Transaction transaction);

    // Transaction -> TransactionDetailDto (for single view)
    public static TransactionDetailDto ToDetailDto(Transaction transaction)
    {
        var dto = TransactionToDetailDtoGenerated(transaction);
        dto.TransactionType = transaction.Amount >= 0 ? "Income" : "Expense";
        dto.Status = transaction.Status.ToString();
        dto.Source = transaction.Source.ToString();
        dto.Tags = string.IsNullOrEmpty(transaction.Tags)
            ? new List<string>()
            : transaction.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        dto.AccountName = transaction.Account?.Name ?? string.Empty;
        dto.AccountType = transaction.Account?.Type.ToString() ?? string.Empty;
        dto.Currency = transaction.Account?.Currency ?? string.Empty;
        dto.CategoryName = transaction.Category?.Name;
        dto.CategoryColor = transaction.Category?.Color;
        dto.CategoryIcon = transaction.Category?.Icon;
        dto.RelatedAccountName = transaction.RelatedTransaction?.Account?.Name;
        dto.Splits = transaction.Splits?.Any() == true
            ? transaction.Splits.Select(ToSplitDto).ToList()
            : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(TransactionDetailDto.TransactionType))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.Status))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.Source))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.Tags))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.AccountName))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.AccountType))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.Currency))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.CategoryName))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.CategoryColor))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.CategoryIcon))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.RelatedAccountName))]
    [MapperIgnoreTarget(nameof(TransactionDetailDto.Splits))]
    private static partial TransactionDetailDto TransactionToDetailDtoGenerated(Transaction transaction);

    // TransactionSplit -> TransactionSplitDto
    public static TransactionSplitDto ToSplitDto(TransactionSplit split)
    {
        var dto = SplitToDtoGenerated(split);
        dto.CategoryName = split.Category?.Name ?? string.Empty;
        dto.CategoryColor = split.Category?.Color;
        return dto;
    }

    [MapperIgnoreTarget(nameof(TransactionSplitDto.CategoryName))]
    [MapperIgnoreTarget(nameof(TransactionSplitDto.CategoryColor))]
    private static partial TransactionSplitDto SplitToDtoGenerated(TransactionSplit split);
}
