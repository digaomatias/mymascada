using Microsoft.AspNetCore.Mvc;
using MediatR;
using MyMascada.Application.Features.Testing.Commands;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for testing and development utilities
/// Only available in development environment
/// </summary>
[ApiController]
[Route("api/[controller]")]
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
        // Only allow in development environment
        if (!_environment.IsDevelopment())
        {
            return BadRequest("This endpoint is only available in development environment");
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