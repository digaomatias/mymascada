using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.Commands;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.CsvImport.Queries;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CsvImportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAICsvAnalysisService _aiAnalysisService;
    private readonly ICurrentUserService _currentUserService;
    private const int MaxFileSizeMB = 10;
    private const int MaxFileSizeBytes = MaxFileSizeMB * 1024 * 1024;

    public CsvImportController(IMediator mediator, IAICsvAnalysisService aiAnalysisService, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _aiAnalysisService = aiAnalysisService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Analyzes CSV structure using AI to suggest column mappings
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<CsvAnalysisResultDto>> AnalyzeCsvWithAI(
        [FromForm] IFormFile file,
        [FromForm] string? accountType = null,
        [FromForm] string? currencyHint = null,
        [FromForm] int sampleSize = 10)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest(new { message = $"File size exceeds {MaxFileSizeMB}MB limit" });
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only CSV files are allowed" });
            }

            // Analyze CSV structure with AI
            using var stream = file.OpenReadStream();
            var result = await _aiAnalysisService.AnalyzeCsvStructureAsync(
                stream, accountType, currencyHint);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                message = "An error occurred while analyzing the CSV file"
            });
        }
    }

    /// <summary>
    /// Imports CSV using confirmed column mappings from AI analysis
    /// </summary>
    [HttpPost("import-with-mappings")]
    public async Task<ActionResult<CsvImportResponse>> ImportWithMappings(
        [FromBody] ImportWithMappingsDto request)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            // Decode CSV content
            byte[] csvData;
            try
            {
                csvData = Convert.FromBase64String(request.CsvContent);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Invalid CSV content format" });
            }

            // Create import command with custom mappings
            var command = new ImportCsvWithMappingsCommand
            {
                UserId = userId,
                AccountId = request.AccountId,
                AccountName = request.AccountName,
                CsvData = csvData,
                FileName = "ai-analyzed.csv",
                Mappings = request.Mappings,
                SkipDuplicates = request.SkipDuplicates,
                AutoCategorize = request.AutoCategorize,
                MaxRows = request.MaxRows
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                message = "An error occurred while importing the CSV file"
            });
        }
    }

    /// <summary>
    /// Validates column mappings against CSV data
    /// </summary>
    [HttpPost("validate-mappings")]
    public async Task<ActionResult<CsvMappingValidationResult>> ValidateMappings(
        [FromForm] IFormFile file,
        [FromForm] string mappingsJson)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            // Parse mappings from JSON
            CsvColumnMappings mappings;
            try
            {
                mappings = System.Text.Json.JsonSerializer.Deserialize<CsvColumnMappings>(mappingsJson) 
                    ?? new CsvColumnMappings();
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(new { message = "Invalid mappings JSON format" });
            }

            using var stream = file.OpenReadStream();
            var result = await _aiAnalysisService.ValidateMappingsAsync(stream, mappings);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                message = "An error occurred while validating mappings"
            });
        }
    }

    [HttpPost("upload")]
    public async Task<ActionResult<CsvImportResponse>> UploadCsv(
        [FromForm] IFormFile file,
        [FromForm] int accountId,
        [FromForm] CsvFormat format = CsvFormat.Generic,
        [FromForm] bool hasHeader = true,
        [FromForm] bool skipDuplicates = true,
        [FromForm] bool autoCategorize = true)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest(new { message = $"File size exceeds {MaxFileSizeMB}MB limit" });
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only CSV files are allowed" });
            }

            // Read file data into byte array
            byte[] csvData;
            using (var stream = file.OpenReadStream())
            {
                csvData = new byte[file.Length];
                await stream.ReadAsync(csvData, 0, (int)file.Length);
            }
            
            var command = new ImportCsvTransactionsCommand
            {
                UserId = _currentUserService.GetUserId(),
                AccountId = accountId,
                CsvData = csvData,
                FileName = file.FileName,
                Format = format,
                HasHeader = hasHeader,
                SkipDuplicates = skipDuplicates,
                AutoCategorize = autoCategorize
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing the file" });
        }
    }

    [HttpGet("formats")]
    [AllowAnonymous]
    public ActionResult<Dictionary<string, object>> GetSupportedFormats()
    {
        var formats = new Dictionary<string, object>
        {
            {
                "Generic", new
                {
                    Name = "Generic CSV",
                    Description = "Standard format: Date, Description, Amount",
                    Columns = new[] { "Date", "Description", "Amount", "Reference (Optional)" }
                }
            },
            {
                "Chase", new
                {
                    Name = "Chase Bank",
                    Description = "Chase Bank CSV export format",
                    Columns = new[] { "Transaction Date", "Post Date", "Description", "Amount" }
                }
            },
            {
                "WellsFargo", new
                {
                    Name = "Wells Fargo",
                    Description = "Wells Fargo CSV export format",
                    Columns = new[] { "Date", "Amount", "Memo", "Description" }
                }
            },
            {
                "BankOfAmerica", new
                {
                    Name = "Bank of America",
                    Description = "Bank of America CSV export format",
                    Columns = new[] { "Date", "Description", "Amount", "Running Balance" }
                }
            },
            {
                "Mint", new
                {
                    Name = "Mint",
                    Description = "Mint transaction export format",
                    Columns = new[] { "Date", "Description", "Category", "Amount" }
                }
            },
            {
                "Quicken", new
                {
                    Name = "Quicken",
                    Description = "Quicken transaction export format",
                    Columns = new[] { "Date", "Description", "Amount", "Category" }
                }
            },
            {
                "ANZ", new
                {
                    Name = "ANZ Bank",
                    Description = "ANZ Bank CSV export format",
                    Columns = new[] { "Type", "Details", "Particulars", "Code", "Reference", "Amount", "Date", "ForeignCurrencyAmount", "ConversionCharge" }
                }
            }
        };

        return Ok(formats);
    }

    [HttpGet("template")]
    public ActionResult DownloadTemplate([FromQuery] CsvFormat format = CsvFormat.Generic)
    {
        var headers = format switch
        {
            CsvFormat.Chase => new[] { "Transaction Date", "Post Date", "Description", "Amount" },
            CsvFormat.WellsFargo => new[] { "Date", "Amount", "Memo", "Description" },
            CsvFormat.BankOfAmerica => new[] { "Date", "Description", "Amount", "Running Balance" },
            CsvFormat.Mint => new[] { "Date", "Description", "Category", "Amount" },
            CsvFormat.Quicken => new[] { "Date", "Description", "Amount", "Category" },
            CsvFormat.ANZ => new[] { "Type", "Details", "Particulars", "Code", "Reference", "Amount", "Date", "ForeignCurrencyAmount", "ConversionCharge" },
            _ => new[] { "Date", "Description", "Amount", "Reference" }
        };

        var sampleData = format switch
        {
            CsvFormat.Chase => new[] { "01/15/2025", "01/16/2025", "STARBUCKS", "-5.25" },
            CsvFormat.WellsFargo => new[] { "01/15/2025", "-5.25", "DEBIT", "STARBUCKS PURCHASE" },
            CsvFormat.BankOfAmerica => new[] { "01/15/2025", "STARBUCKS", "-5.25", "994.75" },
            CsvFormat.Mint => new[] { "01/15/2025", "Starbucks", "Coffee Shops", "-5.25" },
            CsvFormat.Quicken => new[] { "01/15/2025", "Starbucks", "-5.25", "Food & Dining" },
            CsvFormat.ANZ => new[] { "Payment", "Thomaz,Andre", "", "", "", "22.00", "22/06/2025", "", "" },
            _ => new[] { "2025-01-15", "Coffee at Starbucks", "-5.25", "TXN001" }
        };

        var csv = string.Join(",", headers) + "\n" + string.Join(",", sampleData);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        
        return File(bytes, "text/csv", $"template_{format.ToString().ToLower()}.csv");
    }

    [HttpPost("preview")]
    public async Task<ActionResult<CsvParseResult>> PreviewCsv(
        [FromForm] IFormFile file,
        [FromForm] CsvFormat format = CsvFormat.Generic,
        [FromForm] bool hasHeader = true,
        [FromForm] int maxRows = 10)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only CSV files are allowed" });
            }

            // This would require updating the CSV import service to support preview mode
            // For now, return a simplified response
            return Ok(new CsvParseResult
            {
                IsSuccess = true,
                TotalRows = 0,
                ValidRows = 0,
                Transactions = new List<CsvTransactionRow>(),
                Warnings = new List<string> { "Preview functionality not yet implemented" }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while previewing the file" });
        }
    }

    /// <summary>
    /// Detects potential transfers in a list of transactions
    /// </summary>
    [HttpPost("detect-transfers")]
    public async Task<ActionResult<TransferDetectionResult>> DetectTransfers(
        [FromBody] DetectTransfersRequest request)
    {
        try
        {
            var query = new DetectTransfersQuery(request.Transactions, request.Config);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while detecting transfers" });
        }
    }

    /// <summary>
    /// Confirms or rejects a detected transfer candidate
    /// </summary>
    [HttpPost("confirm-transfer")]
    public async Task<ActionResult<bool>> ConfirmTransfer(
        [FromBody] ConfirmTransferCandidateRequest request)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var command = new ConfirmTransferCandidateCommand(
                request.DebitTransactionId,
                request.CreditTransactionId,
                request.IsConfirmed,
                request.Description,
                userId);

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while confirming transfer" });
        }
    }

}


/// <summary>
/// Request for detecting transfers in transactions
/// </summary>
public class DetectTransfersRequest
{
    public List<TransactionDto> Transactions { get; set; } = new();
    public TransferDetectionConfig? Config { get; set; }
}