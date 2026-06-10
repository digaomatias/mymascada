using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.RecurringTransactions.Mappings;

/// <summary>
/// Manual mapping between RecurringTransaction entities and DTOs
/// </summary>
public static class RecurringTransactionMapper
{
    public static RecurringTransactionDto ToDto(RecurringTransaction entity)
    {
        var dto = new RecurringTransactionDto();
        MapSummary(entity, dto);
        return dto;
    }

    public static RecurringTransactionDetailDto ToDetailDto(
        RecurringTransaction entity,
        IEnumerable<RecurringTransactionOccurrence> recentOccurrences)
    {
        var dto = new RecurringTransactionDetailDto();
        MapSummary(entity, dto);
        dto.RecentOccurrences = recentOccurrences.Select(ToOccurrenceDto).ToList();
        return dto;
    }

    public static RecurringTransactionOccurrenceDto ToOccurrenceDto(RecurringTransactionOccurrence occurrence)
    {
        return new RecurringTransactionOccurrenceDto
        {
            Id = occurrence.Id,
            ScheduledDate = occurrence.ScheduledDate,
            Status = occurrence.Status,
            TransactionId = occurrence.TransactionId,
            CreatedAt = occurrence.CreatedAt
        };
    }

    private static void MapSummary(RecurringTransaction entity, RecurringTransactionDto dto)
    {
        dto.Id = entity.Id;
        dto.AccountId = entity.AccountId;
        dto.AccountName = entity.Account?.Name ?? string.Empty;
        dto.CategoryId = entity.CategoryId;
        dto.CategoryName = entity.Category?.Name;
        dto.Description = entity.Description;
        dto.Amount = entity.Amount;
        dto.Frequency = entity.Frequency;
        dto.CustomIntervalDays = entity.CustomIntervalDays;
        dto.StartDate = entity.StartDate;
        dto.EndDate = entity.EndDate;
        dto.NextDueDate = entity.NextDueDate;
        dto.AutoCreate = entity.AutoCreate;
        dto.IsActive = entity.IsActive;
        dto.Notes = entity.Notes;
        dto.CreatedAt = entity.CreatedAt;
        dto.UpdatedAt = entity.UpdatedAt;
    }
}
