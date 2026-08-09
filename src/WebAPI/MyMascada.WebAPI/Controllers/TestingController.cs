using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using MyMascada.Application.Features.Testing.Commands;
using MyMascada.WebAPI.Filters;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for testing and development utilities
/// Only available in development environment: [DevelopmentOnly] returns 404 for
/// every action outside Development, before model binding runs.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
[DevelopmentOnly]
[AllowAnonymous] // pre-auth test utilities; unreachable outside Development
public class TestingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public TestingController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    /// <summary>
    /// Creates a test user with sample data for development and testing
    /// Only available in development environment
    /// </summary>
    [HttpPost("create-test-user")]
    public async Task<ActionResult<CreateTestUserResponse>> CreateTestUser([FromBody] CreateTestUserCommand command)
    {
        // Defense in depth: [DevelopmentOnly] already 404s outside Development
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Health check endpoint for testing — Development only
    /// </summary>
    [HttpGet("health")]
    public ActionResult<object> HealthCheck()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(new
        {
            Status = "Healthy",
            Environment = _environment.EnvironmentName,
            Timestamp = DateTime.UtcNow,
            Message = "Testing controller is available"
        });
    }
}