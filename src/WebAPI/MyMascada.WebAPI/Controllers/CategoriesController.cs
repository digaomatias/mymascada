using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.Commands;
using MyMascada.Application.Features.Categories.DTOs;
using MyMascada.Application.Features.Categories.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICategorySeedingService _categorySeedingService;
    private readonly ICurrentUserService _currentUserService;

    public CategoriesController(IMediator mediator, ICategorySeedingService categorySeedingService, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _categorySeedingService = categorySeedingService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get categories filtered by current transaction filters with transaction counts
    /// </summary>
    [HttpGet("filtered")]
    public async Task<ActionResult<IEnumerable<CategoryWithTransactionCountDto>>> GetFilteredCategories(
        [FromQuery] bool includeSystemCategories = true,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? accountId = null,
        [FromQuery] bool? isReviewed = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] bool? onlyTransfers = null,
        [FromQuery] bool? includeTransfers = null)
    {
        try
        {
            var query = new GetFilteredCategoriesQuery
            {
                UserId = _currentUserService.GetUserId(),
                IncludeSystemCategories = includeSystemCategories,
                IncludeInactive = includeInactive,
                SearchTerm = searchTerm,
                AccountId = accountId,
                IsReviewed = isReviewed,
                StartDate = startDate,
                EndDate = endDate,
                OnlyTransfers = onlyTransfers,
                IncludeTransfers = includeTransfers
            };

            var categories = await _mediator.Send(query);
            return Ok(categories);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving filtered categories." });
        }
    }

    /// <summary>
    /// Get all categories for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(
        [FromQuery] bool includeSystemCategories = true,
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool includeHierarchy = false)
    {
        try
        {
            var query = new GetCategoriesQuery
            {
                UserId = _currentUserService.GetUserId(),
                IncludeSystemCategories = includeSystemCategories,
                IncludeInactive = includeInactive,
                IncludeHierarchy = includeHierarchy
            };

            var categories = await _mediator.Send(query);
            return Ok(categories);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving categories." });
        }
    }

    /// <summary>
    /// Get a specific category by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        try
        {
            var query = new GetCategoryQuery
            {
                CategoryId = id,
                UserId = _currentUserService.GetUserId()
            };

            var category = await _mediator.Send(query);
            
            if (category == null)
            {
                return NotFound(new { message = "Category not found or you don't have permission to access it." });
            }

            return Ok(category);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the category." });
        }
    }

    /// <summary>
    /// Create a new category
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateCategoryCommand
            {
                Name = request.Name,
                Description = request.Description,
                Color = request.Color,
                Icon = request.Icon,
                Type = request.Type,
                ParentCategoryId = request.ParentCategoryId,
                SortOrder = request.SortOrder,
                UserId = _currentUserService.GetUserId()
            };

            var category = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the category." });
        }
    }

    /// <summary>
    /// Update an existing category
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != request.Id)
            {
                return BadRequest(new { message = "ID in URL must match ID in request body." });
            }

            var command = new UpdateCategoryCommand
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                Color = request.Color,
                Icon = request.Icon,
                Type = request.Type,
                ParentCategoryId = request.ParentCategoryId,
                SortOrder = request.SortOrder,
                IsActive = request.IsActive,
                UserId = _currentUserService.GetUserId()
            };

            var category = await _mediator.Send(command);
            return Ok(category);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the category." });
        }
    }

    /// <summary>
    /// Delete a category (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCategory(int id)
    {
        try
        {
            var command = new DeleteCategoryCommand
            {
                CategoryId = id,
                UserId = _currentUserService.GetUserId()
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the category." });
        }
    }

    /// <summary>
    /// Get category statistics (transaction count, total amounts, etc.)
    /// </summary>
    [HttpGet("{id}/statistics")]
    public async Task<ActionResult<CategoryStatisticsDto>> GetCategoryStatistics(int id)
    {
        try
        {
            // First check if category exists and user has access
            var categoryQuery = new GetCategoryQuery
            {
                CategoryId = id,
                UserId = _currentUserService.GetUserId()
            };

            var category = await _mediator.Send(categoryQuery);
            if (category == null)
            {
                return NotFound(new { message = "Category not found or you don't have permission to access it." });
            }

            // For now, return basic statistics - this could be expanded with a dedicated query
            return Ok(new CategoryStatisticsDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                TransactionCount = 0, // TODO: Implement actual statistics
                TotalAmount = 0,
                AverageAmount = 0,
                LastTransactionDate = null,
                FirstTransactionDate = null
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving category statistics." });
        }
    }

    /// <summary>
    /// Initialize default categories for the current user with locale support.
    /// </summary>
    [HttpPost("initialize")]
    public async Task<ActionResult> InitializeDefaultCategories([FromBody] InitializeCategoriesRequest? request = null)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var locale = request?.Locale ?? "en";

            // Validate locale
            var availableLocales = _categorySeedingService.GetAvailableLocales();
            if (!availableLocales.Contains(locale))
            {
                return BadRequest(new { message = $"Unsupported locale '{locale}'. Supported locales: {string.Join(", ", availableLocales)}" });
            }

            // Check if user already has categories
            var hasCategories = await _categorySeedingService.UserHasCategoriesAsync(userId);
            if (hasCategories)
            {
                return BadRequest(new { message = "Categories already exist. Cannot initialize default categories when you already have categories." });
            }

            // Create default categories in the requested locale
            var count = await _categorySeedingService.CreateDefaultCategoriesAsync(userId, locale);

            return Ok(new { message = $"Default categories have been successfully initialized in '{locale}'!", count });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while initializing categories." });
        }
    }

    /// <summary>
    /// Get the list of available locales for category seeding.
    /// </summary>
    [HttpGet("seed-locales")]
    public ActionResult<IReadOnlyList<string>> GetAvailableSeedLocales()
    {
        return Ok(_categorySeedingService.GetAvailableLocales());
    }

    /// <summary>
    /// Backfill CanonicalKey for existing categories that were seeded before the field existed.
    /// This is a one-time migration endpoint that matches English category names to canonical keys.
    /// </summary>
    [HttpPost("backfill-canonical-keys")]
    public async Task<ActionResult<BackfillCanonicalKeysResult>> BackfillCanonicalKeys()
    {
        try
        {
            var command = new BackfillCanonicalKeysCommand();
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while backfilling canonical keys." });
        }
    }
}

public class InitializeCategoriesRequest
{
    public string Locale { get; set; } = "en";
}