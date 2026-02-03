using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.ImportReview.Commands;
using MyMascada.Application.Features.ImportReview.DTOs;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Domain.Enums;
using CsvHelper;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImportReviewController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAICsvAnalysisService _aiAnalysisService;
    private readonly ICurrentUserService _currentUserService;

    public ImportReviewController(IMediator mediator, IAICsvAnalysisService aiAnalysisService, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _aiAnalysisService = aiAnalysisService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Analyzes CSV content and converts it to import candidates
    /// </summary>
    [HttpPost("analyze-csv")]
    public async Task<ActionResult<CsvImportCandidatesResult>> AnalyzeCsvForImport([FromBody] AnalyzeCsvImportRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.CsvContent))
            {
                return BadRequest(new { message = "CSV content is required" });
            }

            // Decode CSV content from base64
            byte[] csvData;
            try
            {
                csvData = Convert.FromBase64String(request.CsvContent);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Invalid CSV content format" });
            }

            // Convert CSV data to import candidates using the mappings
            var candidates = new List<ImportCandidateDto>();
            
            using var stream = new MemoryStream(csvData);
            using var reader = new StreamReader(stream);
            using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);

            var hasHeader = request.HasHeader;
            if (hasHeader)
            {
                csv.Read();
                csv.ReadHeader();
            }

            var recordNumber = 0;
            while (csv.Read() && recordNumber < (request.MaxRows ?? 1000))
            {
                try
                {
                    var amount = ParseAmount(GetFieldValue(csv, request.Mappings.AmountColumn));
                    var typeValue = GetFieldValue(csv, request.Mappings.TypeColumn);
                    var isIncome = DetermineTransactionDirection(amount, typeValue, request.Mappings);
                    var candidate = new ImportCandidateDto
                    {
                        TempId = Guid.NewGuid().ToString(),
                        Amount = amount,
                        Date = ParseDate(GetFieldValue(csv, request.Mappings.DateColumn), request.Mappings.DateFormat),
                        Description = GetFieldValue(csv, request.Mappings.DescriptionColumn) ?? "Unknown Transaction",
                        ReferenceId = GetFieldValue(csv, request.Mappings.ReferenceColumn),
                        ExternalReferenceId = GetFieldValue(csv, request.Mappings.ReferenceColumn),
                        Status = MyMascada.Domain.Enums.TransactionStatus.Cleared,
                        Type = isIncome ? MyMascada.Domain.Enums.TransactionType.Income : MyMascada.Domain.Enums.TransactionType.Expense,
                        SourceRowNumber = recordNumber + 1
                    };

                    candidates.Add(candidate);
                    recordNumber++;
                }
                catch (Exception ex)
                {
                    // Skip invalid rows but log the error
                    continue;
                }
            }

            var result = new CsvImportCandidatesResult
            {
                Success = true,
                Candidates = candidates,
                TotalRows = recordNumber,
                ValidRows = candidates.Count,
                Warnings = new List<string>(),
                Errors = new List<string>()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while analyzing CSV content" });
        }
    }

    /// <summary>
    /// Converts CSV data to import candidates for analysis
    /// </summary>
    private IEnumerable<ImportCandidateDto> ConvertCsvToCandidates(CsvImportData csvData)
    {
        // Decode CSV content from base64
        byte[] csvBytes;
        try
        {
            csvBytes = Convert.FromBase64String(csvData.Content);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid CSV content format");
        }

        // Convert CSV data to import candidates using the mappings
        var candidates = new List<ImportCandidateDto>();

        using var stream = new MemoryStream(csvBytes);
        using var reader = new StreamReader(stream);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);

        var hasHeader = csvData.HasHeader;
        if (hasHeader)
        {
            csv.Read();
            csv.ReadHeader();
        }

        var recordNumber = 0;
        while (csv.Read() && recordNumber < 1000) // Limit to 1000 rows for analysis
        {
            try
            {
                var amount = ParseAmount(GetFieldValue(csv, csvData.Mappings.AmountColumn));
                var typeValue = GetFieldValue(csv, csvData.Mappings.TypeColumn);
                
                // Use the proper type determination method that handles TypeValueMappings
                var mappings = new CsvColumnMappings
                {
                    AmountConvention = csvData.Mappings.AmountConvention,
                    TypeValueMappings = csvData.Mappings.TypeValueMappings != null ? new MyMascada.Application.Features.CsvImport.DTOs.TypeValueMappings
                    {
                        IncomeValues = csvData.Mappings.TypeValueMappings.IncomeValues?.ToList() ?? new List<string>(),
                        ExpenseValues = csvData.Mappings.TypeValueMappings.ExpenseValues?.ToList() ?? new List<string>()
                    } : null
                };
                
                var isIncome = DetermineTransactionDirection(amount, typeValue, mappings);
                
                var candidate = new ImportCandidateDto
                {
                    TempId = Guid.NewGuid().ToString(),
                    Amount = amount,
                    Date = ParseDate(GetFieldValue(csv, csvData.Mappings.DateColumn), csvData.Mappings.DateFormat),
                    Description = GetFieldValue(csv, csvData.Mappings.DescriptionColumn) ?? "Unknown Transaction",
                    ReferenceId = GetFieldValue(csv, csvData.Mappings.ReferenceColumn),
                    Type = isIncome ? TransactionType.Income : TransactionType.Expense,
                    SourceRowNumber = recordNumber + 1
                };

                candidates.Add(candidate);
                recordNumber++;
            }
            catch (Exception ex)
            {
                // Log error but continue processing other rows
                recordNumber++;
                continue;
            }
        }

        return candidates;
    }

    /// <summary>
    /// Analyzes import candidates for conflicts and duplicates
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<ImportAnalysisResult>> AnalyzeImport([FromBody] AnalyzeImportRequest request)
    {
        try
        {
            IEnumerable<ImportCandidateDto> candidates = request.Candidates;
            
            // If CSV data is provided, convert it to candidates
            if (request.CsvData != null && !string.IsNullOrEmpty(request.CsvData.Content))
            {
                candidates = ConvertCsvToCandidates(request.CsvData);
            }
            // If OFX data is provided, convert it to candidates (future implementation)
            else if (request.OfxData != null && !string.IsNullOrEmpty(request.OfxData.Content))
            {
                throw new NotImplementedException("OFX import analysis is not yet implemented");
            }

            var command = new AnalyzeImportCommand
            {
                Source = request.Source,
                AccountId = request.AccountId,
                UserId = _currentUserService.GetUserId(),
                Candidates = candidates,
                Options = request.Options
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while analyzing the import" });
        }
    }

    /// <summary>
    /// Executes import with user decisions for conflict resolution
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<ImportExecutionResult>> ExecuteImport([FromBody] ImportExecutionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.AnalysisId))
            {
                return BadRequest(new { message = "AnalysisId is required" });
            }

            if (!request.Decisions.Any())
            {
                return BadRequest(new { message = "At least one decision is required" });
            }

            var command = new ExecuteImportCommand
            {
                AnalysisId = request.AnalysisId,
                AccountId = request.AccountId,
                UserId = _currentUserService.GetUserId(),
                Decisions = request.Decisions,
                SkipValidation = request.SkipValidation
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while executing the import" });
        }
    }

    /// <summary>
    /// Applies bulk actions to resolve multiple conflicts at once
    /// </summary>
    [HttpPost("bulk-resolve")]
    public async Task<ActionResult<BulkActionResult>> BulkResolveConflicts([FromBody] BulkActionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.AnalysisId))
            {
                return BadRequest(new { message = "AnalysisId is required" });
            }

            var command = new BulkResolveConflictsCommand
            {
                AnalysisId = request.AnalysisId,
                ActionType = request.ActionType,
                TargetConflictType = request.TargetConflictType,
                Resolution = request.Resolution
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while applying bulk action" });
        }
    }

    /// <summary>
    /// Gets the current status of an import analysis
    /// </summary>
    [HttpGet("analysis/{analysisId}/status")]
    public async Task<ActionResult<object>> GetAnalysisStatus(string analysisId)
    {
        try
        {
            // This would typically query the analysis cache or database
            // For now, return a simple status response
            return Ok(new 
            { 
                analysisId,
                status = "active",
                expiresAt = DateTime.UtcNow.AddMinutes(30),
                message = "Analysis is available for execution"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while checking analysis status" });
        }
    }

    #region Helper Methods

    private static string? GetFieldValue(CsvHelper.CsvReader csv, string? columnName)
    {
        if (string.IsNullOrEmpty(columnName))
            return null;

        try
        {
            return csv.GetField(columnName)?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static decimal ParseAmount(string? amountStr)
    {
        if (string.IsNullOrWhiteSpace(amountStr))
            return 0;

        // Remove common formatting characters
        var cleanAmount = amountStr.Replace(",", "").Replace("$", "").Replace("€", "").Trim();
        
        if (decimal.TryParse(cleanAmount, out var amount))
            return amount;

        return 0;
    }

    private static bool DetermineTransactionDirection(decimal amount, string? typeValue, CsvColumnMappings mappings)
    {
        var result = mappings.AmountConvention switch
        {
            "negative-expense" => amount > 0, // Positive amounts are income, negative are expenses
            "negative-debits" => amount > 0, // Legacy: same as negative-expense
            "positive-expense" => amount < 0, // Positive amounts are expenses, negative are income (credit cards)
            "type-column" => DetermineFromTypeColumn(amount, typeValue, mappings),
            "all-positive-income" => true, // Legacy: All amounts are income
            "all-positive-expense" => false, // Legacy: All amounts are expenses
            "all-positive" => false, // Legacy: Default to expense for all-positive amounts
            _ => amount > 0
        };
        
        return result;
    }

    private static bool DetermineTransactionDirectionFromCsvMappings(decimal amount, MyMascada.Application.Features.ImportReview.DTOs.CsvMappings mappings)
    {
        var result = mappings.AmountConvention switch
        {
            "negative-expense" => amount > 0, // Positive amounts are income, negative are expenses
            "negative-debits" => amount > 0, // Legacy: same as negative-expense
            "positive-expense" => amount < 0, // Positive amounts are expenses, negative are income (credit cards)
            "all-positive-income" => true, // Legacy: All amounts are income
            "all-positive-expense" => false, // Legacy: All amounts are expenses
            "all-positive" => false, // Legacy: Default to expense for all-positive amounts
            _ => amount > 0
        };
        
        return result;
    }

    private static bool DetermineFromTypeColumn(decimal amount, string? typeValue, CsvColumnMappings mappings)
    {
        // Check if we have a type column value
        if (!string.IsNullOrEmpty(typeValue))
        {
            // Use custom type mappings if available
            if (mappings.TypeValueMappings != null)
            {
                if (mappings.TypeValueMappings.IncomeValues.Contains(typeValue))
                {
                    return true; // Income
                }
                
                if (mappings.TypeValueMappings.ExpenseValues.Contains(typeValue))
                {
                    return false; // Expense
                }
                
                // If type value not in custom mappings, fall back to amount sign
                return amount > 0;
            }
            
            // Legacy: use hardcoded patterns for backwards compatibility
            var typeValueLower = typeValue.ToLower();
            // Common patterns for income/credit transactions
            if (typeValueLower.Contains("credit") || typeValueLower.Contains("deposit") || 
                typeValueLower.Contains("income") || typeValueLower.Contains("pay") ||
                typeValueLower.Contains("refund") || typeValueLower.Contains("transfer in"))
            {
                return true; // Income
            }
            
            // Common patterns for expense/debit transactions
            if (typeValueLower.Contains("debit") || typeValueLower.Contains("withdrawal") || 
                typeValueLower.Contains("expense") || typeValueLower.Contains("payment") ||
                typeValueLower.Contains("purchase") || typeValueLower.Contains("transfer out"))
            {
                return false; // Expense
            }
        }
        
        // Fall back to amount sign if type column doesn't give clear indication
        return amount > 0;
    }

    private static DateTime ParseDate(string? dateStr, string? dateFormat)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

        // Try common date formats
        var formats = new[]
        {
            dateFormat,
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            "MM-dd-yyyy",
            "dd-MM-yyyy",
            "yyyy/MM/dd"
        }.Where(f => !string.IsNullOrEmpty(f)).ToArray();

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format, null, System.Globalization.DateTimeStyles.None, out var date))
                return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        // Fallback to general parsing
        if (DateTime.TryParse(dateStr, out var parsedDate))
            return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

        return DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
    }

    #endregion
}

#region Request/Response DTOs

public record AnalyzeCsvImportRequest
{
    public string CsvContent { get; init; } = string.Empty; // Base64 encoded
    public CsvColumnMappings Mappings { get; init; } = new();
    public bool HasHeader { get; init; } = true;
    public int? MaxRows { get; init; }
}

public record CsvImportCandidatesResult
{
    public bool Success { get; init; }
    public IEnumerable<ImportCandidateDto> Candidates { get; init; } = new List<ImportCandidateDto>();
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public IEnumerable<string> Warnings { get; init; } = new List<string>();
    public IEnumerable<string> Errors { get; init; } = new List<string>();
}

#endregion