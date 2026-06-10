using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Devices.Commands;
using MyMascada.Application.Features.Devices.DTOs;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Mobile push notification device registry (FCM tokens).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IMediator mediator, ICurrentUserService currentUserService, ILogger<DevicesController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Register (or refresh) an FCM device token for the current user.
    /// Idempotent — re-registering the same token updates its LastSeenAt.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DeviceDto>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new RegisterDeviceCommand
            {
                UserId = _currentUserService.GetUserId(),
                Token = request.Token,
                Platform = request.Platform
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
            _logger.LogError(ex, "Error registering device for user");
            return StatusCode(500, new { message = "An error occurred while registering the device." });
        }
    }

    /// <summary>
    /// Unregister an FCM device token (e.g. on logout). Idempotent.
    /// The token travels in the request body so it never appears in URL/proxy logs.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> UnregisterDevice(
        [FromBody] UnregisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new UnregisterDeviceCommand
            {
                UserId = _currentUserService.GetUserId(),
                Token = request.Token
            };

            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
            _logger.LogError(ex, "Error unregistering device for user");
            return StatusCode(500, new { message = "An error occurred while unregistering the device." });
        }
    }
}
