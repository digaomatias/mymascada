using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Common.Interfaces;

public interface IImportAnalysisService
{
    Task<ImportAnalysisResult> AnalyzeImportAsync(AnalyzeImportRequest request);
    Task<ImportExecutionResult> ExecuteImportAsync(ImportExecutionRequest request);
    Task<BulkActionResult> ApplyBulkActionAsync(BulkActionRequest request);
}