using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AiChat.Commands;
using MyMascada.Application.Features.AiChat.DTOs;
using MyMascada.Application.Features.AiChat.Queries;
using MyMascada.WebAPI.Extensions;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/ai-chat")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IChatMessageRepository _chatMessageRepository;

    public AiChatController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IChatMessageRepository chatMessageRepository)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _chatMessageRepository = chatMessageRepository;
    }

    [HttpPost("messages")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Standard)]
    public async Task<ActionResult<SendChatMessageResponse>> SendMessage(
        [FromBody] SendChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { Error = "Message content is required." });

        if (request.Content.Length > 5000)
            return BadRequest(new { Error = "Message is too long. Maximum 5000 characters." });

        var result = await _mediator.Send(new SendChatMessageCommand { Content = request.Content });
        return Ok(result);
    }

    [HttpGet("messages")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.ReadOnly)]
    public async Task<ActionResult<ChatHistoryResponse>> GetHistory(
        [FromQuery] int limit = 50, [FromQuery] int? before = null)
    {
        var result = await _mediator.Send(new GetChatHistoryQuery { Limit = limit, BeforeId = before });
        return Ok(result);
    }

    [HttpDelete("messages")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Standard)]
    public async Task<IActionResult> ClearHistory()
    {
        var userId = _currentUserService.GetUserId();
        await _chatMessageRepository.DeleteAllForUserAsync(userId);
        return Ok(new { Message = "Chat history cleared." });
    }
}
