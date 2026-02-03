using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Commands;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Application.Features.BankConnections.Queries;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for managing bank connections and synchronization.
/// Provides endpoints for connecting bank accounts via providers like Akahu,
/// managing those connections, and triggering transaction synchronization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BankConnectionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public BankConnectionsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets all bank connections for the current user.
    /// </summary>
    /// <returns>List of bank connections</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BankConnectionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BankConnectionDto>>> GetBankConnections()
    {
        var query = new GetBankConnectionsQuery(_currentUserService.GetUserId());
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Gets detailed information about a specific bank connection.
    /// </summary>
    /// <param name="id">The bank connection ID</param>
    /// <returns>Bank connection details including recent sync history</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BankConnectionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BankConnectionDetailDto>> GetBankConnection(int id)
    {
        var query = new GetBankConnectionQuery(_currentUserService.GetUserId(), id);
        var result = await _mediator.Send(query);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets the list of available bank providers that can be connected.
    /// </summary>
    /// <returns>List of available bank providers with their capabilities</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<BankProviderInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BankProviderInfo>>> GetAvailableProviders()
    {
        var query = new GetAvailableProvidersQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Initiates the Akahu OAuth flow.
    /// Returns an authorization URL to redirect the user to Akahu for authentication.
    /// </summary>
    /// <param name="request">Optional request parameters including email hint</param>
    /// <returns>Authorization URL and state parameter for CSRF protection</returns>
    [HttpPost("akahu/initiate")]
    [ProducesResponseType(typeof(InitiateConnectionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<InitiateConnectionResult>> InitiateAkahuConnection([FromBody] InitiateAkahuRequest? request)
    {
        var command = new InitiateAkahuConnectionCommand(
            _currentUserService.GetUserId(),
            request?.Email
        );
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Checks if the current user has stored Akahu credentials.
    /// </summary>
    /// <returns>Credential status</returns>
    [HttpGet("akahu/has-credentials")]
    [ProducesResponseType(typeof(HasAkahuCredentialsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HasAkahuCredentialsResponse>> HasAkahuCredentials()
    {
        var query = new HasAkahuCredentialsQuery(_currentUserService.GetUserId());
        var result = await _mediator.Send(query);
        return Ok(new HasAkahuCredentialsResponse(result));
    }

    /// <summary>
    /// Saves or updates the user's Akahu credentials.
    /// Validates the credentials against the Akahu API before saving.
    /// </summary>
    /// <param name="request">The App Token and User Token from Akahu Personal App</param>
    /// <returns>Validation result and available accounts if successful</returns>
    [HttpPost("akahu/credentials")]
    [ProducesResponseType(typeof(SaveAkahuCredentialsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SaveAkahuCredentialsResult>> SaveAkahuCredentials([FromBody] SaveAkahuCredentialsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AppIdToken))
        {
            return BadRequest(new { message = "App Token is required" });
        }

        if (string.IsNullOrWhiteSpace(request.UserToken))
        {
            return BadRequest(new { message = "User Token is required" });
        }

        var command = new SaveAkahuCredentialsCommand(
            _currentUserService.GetUserId(),
            request.AppIdToken.Trim(),
            request.UserToken.Trim()
        );
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Completes linking an Akahu account to a MyMascada account.
    /// Uses the user's stored Akahu credentials.
    /// </summary>
    /// <param name="request">Account selection parameters</param>
    /// <returns>The created bank connection</returns>
    [HttpPost("akahu/complete")]
    [ProducesResponseType(typeof(BankConnectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BankConnectionDto>> CompleteAkahuConnection([FromBody] CompleteAkahuRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AkahuAccountId))
        {
            return BadRequest(new { message = "Akahu account ID is required" });
        }

        if (request.AccountId <= 0)
        {
            return BadRequest(new { message = "MyMascada account ID is required" });
        }

        try
        {
            var command = new CompleteAkahuConnectionCommand(
                _currentUserService.GetUserId(),
                request.AccountId,
                request.AkahuAccountId
            );
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetBankConnection), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Exchanges an OAuth code for an access token and returns available Akahu accounts.
    /// NOTE: This endpoint is for Production App OAuth mode only. For Personal App mode,
    /// use the /akahu/credentials endpoint to store credentials and /akahu/accounts to get accounts.
    /// </summary>
    /// <param name="request">OAuth callback code, state, and app token</param>
    /// <returns>List of Akahu accounts and the access token</returns>
    [HttpPost("akahu/exchange")]
    [ProducesResponseType(typeof(ExchangeAkahuCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExchangeAkahuCodeResponse>> ExchangeAkahuCode([FromBody] ExchangeAkahuCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Authorization code is required" });
        }

        if (string.IsNullOrWhiteSpace(request.State))
        {
            return BadRequest(new { message = "State parameter is required" });
        }

        if (string.IsNullOrWhiteSpace(request.AppIdToken))
        {
            return BadRequest(new { message = "App Token is required for OAuth mode" });
        }

        try
        {
            var query = new ExchangeAkahuCodeQuery(
                _currentUserService.GetUserId(),
                request.Code,
                request.State,
                request.AppIdToken
            );
            var result = await _mediator.Send(query);
            return Ok(new ExchangeAkahuCodeResponse(result.Accounts, result.AccessToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets available Akahu accounts that can be linked.
    /// Uses the user's stored Akahu credentials.
    /// </summary>
    /// <returns>List of Akahu accounts with their link status</returns>
    [HttpGet("akahu/accounts")]
    [ProducesResponseType(typeof(IEnumerable<AkahuAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<AkahuAccountDto>>> GetAvailableAkahuAccounts()
    {
        try
        {
            var query = new GetAvailableAkahuAccountsQuery(_currentUserService.GetUserId());
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Disconnects and removes a bank connection.
    /// This will revoke the provider token if applicable and delete the connection record.
    /// </summary>
    /// <param name="id">The bank connection ID to disconnect</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DisconnectBankConnection(int id)
    {
        try
        {
            var command = new DisconnectBankConnectionCommand(_currentUserService.GetUserId(), id);
            var result = await _mediator.Send(command);

            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Triggers a manual sync for a specific bank connection.
    /// Fetches new transactions from the bank provider.
    /// </summary>
    /// <param name="id">The bank connection ID to sync</param>
    /// <returns>Sync result with statistics</returns>
    [HttpPost("{id}/sync")]
    [ProducesResponseType(typeof(BankSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BankSyncResultDto>> SyncBankConnection(int id)
    {
        try
        {
            var command = new SyncBankConnectionCommand(_currentUserService.GetUserId(), id);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Triggers a manual sync for all active bank connections of the current user.
    /// </summary>
    /// <returns>List of sync results for each connection</returns>
    [HttpPost("sync-all")]
    [ProducesResponseType(typeof(IEnumerable<BankSyncResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BankSyncResultDto>>> SyncAllConnections()
    {
        var command = new SyncAllConnectionsCommand(_currentUserService.GetUserId());
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Gets the sync history for a specific bank connection.
    /// </summary>
    /// <param name="id">The bank connection ID</param>
    /// <param name="limit">Maximum number of sync logs to return (default: 20, max: 100)</param>
    /// <returns>List of sync log entries</returns>
    [HttpGet("{id}/sync-history")]
    [ProducesResponseType(typeof(IEnumerable<BankSyncLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<BankSyncLogDto>>> GetSyncHistory(int id, [FromQuery] int limit = 20)
    {
        try
        {
            var query = new GetSyncHistoryQuery(_currentUserService.GetUserId(), id, limit);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

/// <summary>
/// Request DTO for initiating an Akahu OAuth connection.
/// </summary>
public record InitiateAkahuRequest(
    /// <summary>
    /// Optional email hint to pre-fill in the Akahu login form.
    /// </summary>
    [property: JsonPropertyName("email")] string? Email = null
);

/// <summary>
/// Response DTO for checking Akahu credentials status.
/// </summary>
public record HasAkahuCredentialsResponse(
    /// <summary>
    /// Whether the user has stored Akahu credentials.
    /// </summary>
    [property: JsonPropertyName("hasCredentials")] bool HasCredentials
);

/// <summary>
/// Request DTO for saving Akahu credentials.
/// </summary>
public record SaveAkahuCredentialsRequest(
    /// <summary>
    /// The Akahu App Token (app_token_xxx) from my.akahu.nz/developers.
    /// </summary>
    [property: JsonPropertyName("appIdToken")] string AppIdToken,

    /// <summary>
    /// The Akahu User Token (user_token_xxx) from my.akahu.nz/developers.
    /// </summary>
    [property: JsonPropertyName("userToken")] string UserToken
);

/// <summary>
/// Request DTO for completing an Akahu account link (Personal App mode).
/// </summary>
public record CompleteAkahuRequest(
    /// <summary>
    /// The MyMascada account ID to link this Akahu account to.
    /// </summary>
    [property: JsonPropertyName("accountId")] int AccountId,

    /// <summary>
    /// The Akahu account ID (acc_xxx) to link.
    /// </summary>
    [property: JsonPropertyName("akahuAccountId")] string AkahuAccountId
);

/// <summary>
/// Request DTO for exchanging an OAuth code for Akahu accounts (Production App mode).
/// </summary>
public record ExchangeAkahuCodeRequest(
    /// <summary>
    /// The authorization code returned from Akahu OAuth callback.
    /// </summary>
    [property: JsonPropertyName("code")] string Code,

    /// <summary>
    /// The state parameter returned from Akahu OAuth callback (for CSRF validation).
    /// </summary>
    [property: JsonPropertyName("state")] string State,

    /// <summary>
    /// The Akahu App Token for authentication.
    /// </summary>
    [property: JsonPropertyName("appIdToken")] string AppIdToken
);

/// <summary>
/// Response DTO for the OAuth code exchange, containing available accounts.
/// </summary>
public record ExchangeAkahuCodeResponse(
    /// <summary>
    /// List of available Akahu accounts that can be linked.
    /// </summary>
    [property: JsonPropertyName("accounts")] IEnumerable<AkahuAccountDto> Accounts,

    /// <summary>
    /// The access token for subsequent API calls.
    /// </summary>
    [property: JsonPropertyName("accessToken")] string AccessToken
);
