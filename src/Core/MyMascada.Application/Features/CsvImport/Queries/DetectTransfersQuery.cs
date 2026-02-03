using MediatR;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.CsvImport.Queries;

/// <summary>
/// Query to detect potential transfers in a list of transactions
/// </summary>
public record DetectTransfersQuery(
    List<TransactionDto> Transactions,
    TransferDetectionConfig? Config = null
) : IRequest<TransferDetectionResult>;