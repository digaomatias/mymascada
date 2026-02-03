using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.Commands;
using MyMascada.Application.Features.BankCategoryMappings.DTOs;
using MyMascada.Application.Features.BankCategoryMappings.Queries;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for managing bank category mappings.
/// These mappings associate bank-provided categories (e.g., Akahu categories)
/// with the user's MyMascada categories for automatic categorization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BankCategoryMappingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public BankCategoryMappingsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all bank category mappings for the current user.
    /// </summary>
    /// <param name="providerId">Optional: Filter by provider ID (e.g., "akahu")</param>
    /// <param name="activeOnly">Whether to include only active mappings (default: true)</param>
    [HttpGet]
    public async Task<ActionResult<BankCategoryMappingsListDto>> GetMappings(
        [FromQuery] string? providerId = null,
        [FromQuery] bool activeOnly = true)
    {
        try
        {
            var query = new GetBankCategoryMappingsQuery
            {
                UserId = _currentUserService.GetUserId(),
                ProviderId = providerId,
                ActiveOnly = activeOnly
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
            return StatusCode(500, new { message = "An error occurred while retrieving bank category mappings." });
        }
    }

    /// <summary>
    /// Get a single bank category mapping by ID.
    /// </summary>
    /// <param name="id">The mapping ID</param>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BankCategoryMappingDto>> GetMapping(int id)
    {
        try
        {
            var query = new GetBankCategoryMappingByIdQuery
            {
                MappingId = id,
                UserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(query);

            if (result == null)
            {
                return NotFound(new { message = $"Bank category mapping with ID {id} not found." });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the bank category mapping." });
        }
    }

    /// <summary>
    /// Create a new bank category mapping.
    /// </summary>
    /// <param name="dto">The mapping details</param>
    [HttpPost]
    public async Task<ActionResult<BankCategoryMappingDto>> CreateMapping([FromBody] CreateBankCategoryMappingDto dto)
    {
        try
        {
            var command = new CreateBankCategoryMappingCommand
            {
                UserId = _currentUserService.GetUserId(),
                BankCategoryName = dto.BankCategoryName,
                ProviderId = dto.ProviderId,
                CategoryId = dto.CategoryId
            };

            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetMapping), new { id = result.Id }, result);
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
            return StatusCode(500, new { message = "An error occurred while creating the bank category mapping." });
        }
    }

    /// <summary>
    /// Update an existing bank category mapping.
    /// </summary>
    /// <param name="id">The mapping ID</param>
    /// <param name="dto">The updated mapping details</param>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BankCategoryMappingDto>> UpdateMapping(int id, [FromBody] UpdateBankCategoryMappingDto dto)
    {
        try
        {
            var command = new UpdateBankCategoryMappingCommand
            {
                MappingId = id,
                UserId = _currentUserService.GetUserId(),
                CategoryId = dto.CategoryId
            };

            var result = await _mediator.Send(command);

            if (result == null)
            {
                return NotFound(new { message = $"Bank category mapping with ID {id} not found." });
            }

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
            return StatusCode(500, new { message = "An error occurred while updating the bank category mapping." });
        }
    }

    /// <summary>
    /// Delete a bank category mapping.
    /// </summary>
    /// <param name="id">The mapping ID</param>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteMapping(int id)
    {
        try
        {
            var command = new DeleteBankCategoryMappingCommand
            {
                MappingId = id,
                UserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(command);

            if (!result)
            {
                return NotFound(new { message = $"Bank category mapping with ID {id} not found." });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the bank category mapping." });
        }
    }

    /// <summary>
    /// Set the exclusion status for a bank category mapping.
    /// Excluded categories will not be used for automatic categorization,
    /// allowing other handlers (ML, LLM) to process the transaction instead.
    /// </summary>
    /// <param name="id">The mapping ID</param>
    /// <param name="dto">The exclusion status to set</param>
    [HttpPatch("{id:int}/exclude")]
    public async Task<ActionResult<BankCategoryMappingDto>> SetExclusion(int id, [FromBody] SetExclusionRequestDto dto)
    {
        try
        {
            var command = new SetBankCategoryExclusionCommand
            {
                MappingId = id,
                UserId = _currentUserService.GetUserId(),
                IsExcluded = dto.IsExcluded
            };

            var result = await _mediator.Send(command);

            if (result == null)
            {
                return NotFound(new { message = $"Bank category mapping with ID {id} not found." });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while setting exclusion status." });
        }
    }
}
