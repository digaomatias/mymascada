using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UserData.Commands;
using MyMascada.Application.Features.UserData.DTOs;
using MyMascada.Application.Features.UserData.Queries;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for LGPD/GDPR compliance features including data export and account deletion.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserDataController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UserDataController> _logger;

    public UserDataController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        ILogger<UserDataController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Export all user data for LGPD/GDPR Article 20 compliance (right to data portability).
    /// Returns a JSON file containing all personal data associated with the user account.
    /// </summary>
    /// <returns>JSON file with all user data</returns>
    [HttpGet("export")]
    [EnableRateLimiting("standard")]
    [ProducesResponseType(typeof(UserDataExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportUserData()
    {
        var userId = _currentUserService.GetUserId();
        _logger.LogInformation("User {UserId} requested data export (LGPD/GDPR)", userId);

        try
        {
            var query = new ExportUserDataQuery { UserId = userId };
            var result = await _mediator.Send(query);

            // Serialize with pretty printing for human readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(result, options);
            var bytes = Encoding.UTF8.GetBytes(jsonContent);

            var fileName = $"mymascada-data-export-{DateTime.UtcNow:yyyy-MM-dd}.json";

            _logger.LogInformation(
                "Data export completed for user {UserId}: {Size} bytes, {Accounts} accounts, {Transactions} transactions",
                userId, bytes.Length, result.Summary.TotalAccounts, result.Summary.TotalTransactions);

            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export data for user {UserId}", userId);
            return StatusCode(500, "An error occurred while exporting your data. Please try again later.");
        }
    }

    /// <summary>
    /// Get a summary of all user data (for preview before export or deletion).
    /// </summary>
    /// <returns>Data summary without actual data</returns>
    [HttpGet("summary")]
    [EnableRateLimiting("standard")]
    [ProducesResponseType(typeof(DataSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DataSummaryDto>> GetDataSummary()
    {
        var userId = _currentUserService.GetUserId();

        try
        {
            var query = new ExportUserDataQuery { UserId = userId };
            var result = await _mediator.Send(query);

            return Ok(result.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data summary for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving your data summary.");
        }
    }

    /// <summary>
    /// Permanently delete the user account and all associated data.
    /// This implements the LGPD/GDPR "right to be forgotten" (Article 17).
    /// WARNING: This operation is IRREVERSIBLE. All data will be permanently deleted.
    /// </summary>
    /// <param name="confirmation">Must be "DELETE" to confirm the operation</param>
    /// <returns>Summary of deleted data</returns>
    [HttpDelete("account")]
    [EnableRateLimiting("authentication")]
    [ProducesResponseType(typeof(UserDeletionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDeletionResultDto>> DeleteAccount([FromQuery] string confirmation)
    {
        var userId = _currentUserService.GetUserId();

        // Require explicit confirmation to prevent accidental deletion
        if (string.IsNullOrWhiteSpace(confirmation) || confirmation != "DELETE")
        {
            _logger.LogWarning("User {UserId} attempted account deletion without proper confirmation", userId);
            return BadRequest("To delete your account, you must provide confirmation=DELETE as a query parameter. This action is IRREVERSIBLE.");
        }

        _logger.LogWarning("User {UserId} initiated complete account deletion (LGPD/GDPR right to be forgotten)", userId);

        try
        {
            var command = new DeleteUserAccountCommand { UserId = userId };
            var result = await _mediator.Send(command);

            if (result.Success)
            {
                _logger.LogWarning(
                    "Account deletion completed for user {UserId}: {Accounts} accounts, {Transactions} transactions deleted",
                    userId, result.AccountsDeleted, result.TransactionsDeleted);

                return Ok(result);
            }
            else
            {
                _logger.LogError("Account deletion failed for user {UserId}: {Error}", userId, result.ErrorMessage);
                return StatusCode(500, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account for user {UserId}", userId);
            return StatusCode(500, new UserDeletionResultDto
            {
                UserId = userId,
                DeletedAt = DateTime.UtcNow,
                Success = false,
                ErrorMessage = "An unexpected error occurred while deleting your account. Please contact support."
            });
        }
    }
}
