using MediatR;
using MyMascada.Application.Features.CsvImport.DTOs;

namespace MyMascada.Application.Features.CsvImport.Commands;

/// <summary>
/// Command to confirm or reject a detected transfer candidate
/// </summary>
public record ConfirmTransferCandidateCommand(
    int DebitTransactionId,
    int CreditTransactionId,
    bool IsConfirmed,
    string? Description = null,
    Guid? UserId = null
) : IRequest<bool>;