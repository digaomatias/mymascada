using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.OfxImport.Commands;
using System.Text;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for handling OFX file imports
/// </summary>
[ApiController]
[Route("api/ofx-import")]
[Authorize]
public class OfxImportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<OfxImportController> _logger;
    private readonly ICurrentUserService _currentUserService;

    public OfxImportController(IMediator mediator, ILogger<OfxImportController> logger, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Import transactions from an OFX file
    /// </summary>
    /// <param name="file">The OFX file to import</param>
    /// <param name="accountId">Optional account ID to associate transactions with</param>
    /// <param name="createAccount">Whether to create a new account if none specified</param>
    /// <param name="accountName">Name for the new account if creating one</param>
    /// <returns>Import result with statistics</returns>
    [HttpPost("import")]
    public async Task<IActionResult> ImportOfxFile(
        IFormFile file,
        [FromForm] int? accountId = null,
        [FromForm] bool createAccount = false,
        [FromForm] string? accountName = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Please provide a valid OFX file" });
            }

            // Validate file type
            var allowedExtensions = new[] { ".ofx", ".qfx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = "Invalid file type. Please upload an OFX or QFX file." });
            }

            // Check file size (limit to 10MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { message = "File size exceeds 10MB limit" });
            }

            // Read file content
            string content;
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { message = "File appears to be empty" });
            }

            // Get user ID from claims
            var userId = _currentUserService.GetUserId();

            _logger.LogInformation("Starting OFX import for user {UserId}, file: {FileName}", userId, file.FileName);

            // Execute import command
            var command = new ImportOfxFileCommand
            {
                FileName = file.FileName,
                Content = content,
                AccountId = accountId,
                CreateAccountIfNotExists = createAccount,
                AccountName = accountName,
                UserId = userId.ToString()
            };

            var result = await _mediator.Send(command);

            if (result.Success)
            {
                _logger.LogInformation("OFX import completed successfully for user {UserId}. " +
                    "Imported: {ImportedCount}, Skipped: {SkippedCount}, Duplicates: {DuplicateCount}",
                    userId, result.ImportedTransactionsCount, result.SkippedTransactionsCount, result.DuplicateTransactionsCount);

                return Ok(result);
            }
            else
            {
                _logger.LogWarning("OFX import failed for user {UserId}: {Message}", userId, result.Message);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OFX import for file {FileName}", file?.FileName);
            return StatusCode(500, new { message = "An error occurred during import" });
        }
    }

    /// <summary>
    /// Validate an OFX file without importing
    /// </summary>
    /// <param name="file">The OFX file to validate</param>
    /// <param name="includeTransactions">Whether to include transaction details in response</param>
    /// <returns>Validation result with file information and optionally transaction details</returns>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateOfxFile(IFormFile file, [FromForm] bool includeTransactions = false)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Please provide a valid OFX file" });
            }

            // Validate file type
            var allowedExtensions = new[] { ".ofx", ".qfx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = "Invalid file type. Please upload an OFX or QFX file." });
            }

            // Read file content
            string content;
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }

            // Parse but don't import
            var parser = HttpContext.RequestServices.GetRequiredService<Infrastructure.Services.OfxImport.OfxParserService>();
            var parseResult = parser.ParseOfxFile(content);

            var response = new
            {
                success = parseResult.Success,
                message = parseResult.Message,
                errors = parseResult.Errors,
                warnings = parseResult.Warnings,
                accountInfo = parseResult.AccountInfo,
                transactionCount = parseResult.Transactions.Count,
                statementPeriod = new
                {
                    startDate = parseResult.StatementStartDate,
                    endDate = parseResult.StatementEndDate
                },
                // Include transaction details if requested (for reconciliation)
                transactions = includeTransactions ? parseResult.Transactions.Select(t => new
                {
                    transactionId = t.TransactionId,
                    amount = t.Amount,
                    transactionDate = t.PostedDate,
                    description = t.Name,
                    memo = t.Memo,
                    transactionType = t.TransactionType,
                    checkNumber = t.CheckNumber,
                    referenceNumber = t.ReferenceNumber
                }).ToList() : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating OFX file {FileName}", file?.FileName);
            return StatusCode(500, new { message = "Error validating file" });
        }
    }
}