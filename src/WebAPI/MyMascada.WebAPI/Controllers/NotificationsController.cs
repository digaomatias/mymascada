using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Notifications.Commands;
using MyMascada.Application.Features.Notifications.DTOs;
using MyMascada.Application.Features.Notifications.Queries;
using MyMascada.Domain.Enums;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(IMediator mediator, ICurrentUserService currentUserService, ILogger<NotificationsController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated notifications for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] NotificationType? type = null,
        [FromQuery] bool? isRead = null)
    {
        try
        {
            var query = new GetNotificationsQuery
            {
                UserId = _currentUserService.GetUserId(),
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 1, 50),
                Type = type,
                IsRead = isRead
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user");
            return StatusCode(500, new { message = "An error occurred while retrieving notifications." });
        }
    }

    /// <summary>
    /// Get unread notification count for badge display
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount()
    {
        try
        {
            var query = new GetUnreadCountQuery
            {
                UserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notification count for user");
            return StatusCode(500, new { message = "An error occurred while getting unread count." });
        }
    }

    /// <summary>
    /// Mark a single notification as read
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var command = new MarkNotificationReadCommand
            {
                NotificationId = id,
                UserId = _currentUserService.GetUserId()
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", id);
            return StatusCode(500, new { message = "An error occurred while marking notification as read." });
        }
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        try
        {
            var command = new MarkAllNotificationsReadCommand
            {
                UserId = _currentUserService.GetUserId()
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user");
            return StatusCode(500, new { message = "An error occurred while marking all notifications as read." });
        }
    }

    /// <summary>
    /// Delete/dismiss a notification
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteNotification(Guid id)
    {
        try
        {
            var command = new DeleteNotificationCommand
            {
                NotificationId = id,
                UserId = _currentUserService.GetUserId()
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the notification." });
        }
    }

    /// <summary>
    /// Get notification preferences
    /// </summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferenceDto>> GetPreferences()
    {
        try
        {
            var query = new GetNotificationPreferencesQuery
            {
                UserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification preferences for user");
            return StatusCode(500, new { message = "An error occurred while retrieving preferences." });
        }
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    [HttpPut("preferences")]
    public async Task<ActionResult<NotificationPreferenceDto>> UpdatePreferences(
        [FromBody] UpdateNotificationPreferenceRequest request)
    {
        try
        {
            var command = new UpdateNotificationPreferencesCommand
            {
                UserId = _currentUserService.GetUserId(),
                ChannelPreferences = request.ChannelPreferences,
                QuietHoursStart = request.QuietHoursStart,
                QuietHoursEnd = request.QuietHoursEnd,
                QuietHoursTimezone = request.QuietHoursTimezone,
                LargeTransactionThreshold = request.LargeTransactionThreshold,
                BudgetAlertPercentage = request.BudgetAlertPercentage,
                RunwayWarningMonths = request.RunwayWarningMonths
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences for user");
            return StatusCode(500, new { message = "An error occurred while updating preferences." });
        }
    }
}
