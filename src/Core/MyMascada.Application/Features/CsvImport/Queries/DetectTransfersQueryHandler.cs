using MediatR;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.CsvImport.Services;

namespace MyMascada.Application.Features.CsvImport.Queries;

/// <summary>
/// Handler for detecting potential transfers in transactions
/// </summary>
public class DetectTransfersQueryHandler : IRequestHandler<DetectTransfersQuery, TransferDetectionResult>
{
    public Task<TransferDetectionResult> Handle(DetectTransfersQuery request, CancellationToken cancellationToken)
    {
        var service = new TransferDetectionService(request.Config);
        var result = service.DetectPotentialTransfers(request.Transactions);
        return Task.FromResult(result);
    }
}