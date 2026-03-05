using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;
using MyMascada.Application.Features.Reports.Queries;
using MyMascada.Application.Features.UpcomingBills.DTOs;
using MyMascada.Application.Features.UpcomingBills.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public ReportsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get dashboard summary with total balance, monthly income/expenses, and recent transactions
    /// </summary>
    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary()
    {
        var query = new GetDashboardSummaryQuery
        {
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get account balances summary
    /// </summary>
    [HttpGet("account-balances")]
    public async Task<ActionResult<IEnumerable<AccountBalanceReportDto>>> GetAccountBalances()
    {
        var query = new GetAccountBalancesReportQuery
        {
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get monthly summary for specific month and year
    /// </summary>
    [HttpGet("monthly-summary")]
    public async Task<ActionResult<MonthlySummaryDto>> GetMonthlySummary([FromQuery] int year, [FromQuery] int month)
    {
        // Basic validation
        if (year < 1900 || year > 3000)
        {
            return BadRequest($"Invalid year: {year}. Year must be between 1900 and 3000.");
        }

        if (month < 1 || month > 12)
        {
            return BadRequest($"Invalid month: {month}. Month must be between 1 and 12.");
        }

        try
        {
            var query = new GetMonthlySummaryQuery
            {
                UserId = _currentUserService.GetUserId(),
                Year = year,
                Month = month
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest($"Invalid date parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log the exception (you might want to add proper logging here)
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Get cashflow history (income, expenses, net) per month
    /// </summary>
    /// <param name="months">Number of months to return (default: 7, max: 24)</param>
    [HttpGet("cashflow-history")]
    public async Task<ActionResult<CashflowHistoryDto>> GetCashflowHistory([FromQuery] int months = 7)
    {
        try
        {
            if (months < 1 || months > 24)
            {
                return BadRequest("months must be between 1 and 24");
            }

            var query = new GetCashflowHistoryQuery
            {
                UserId = _currentUserService.GetUserId(),
                Months = months
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Get category spending trends over time
    /// </summary>
    /// <param name="startDate">Start date for the trend period (defaults to 12 months ago)</param>
    /// <param name="endDate">End date for the trend period (defaults to current date)</param>
    /// <param name="categoryIds">Optional comma-separated list of category IDs to filter</param>
    /// <param name="limit">Optional limit on number of categories to return</param>
    [HttpGet("category-trends")]
    public async Task<ActionResult<CategoryTrendsResponseDto>> GetCategoryTrends(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? categoryIds,
        [FromQuery] int? limit)
    {
        try
        {
            // Parse category IDs if provided
            List<int>? categoryIdList = null;
            if (!string.IsNullOrWhiteSpace(categoryIds))
            {
                categoryIdList = categoryIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();
            }

            var query = new GetCategoryTrendsQuery
            {
                UserId = _currentUserService.GetUserId(),
                StartDate = startDate,
                EndDate = endDate,
                CategoryIds = categoryIdList,
                Limit = limit
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Get analytics summary with trends, savings rate, and yearly comparisons
    /// </summary>
    /// <param name="period">Period type: "year", "quarter", "month", or "all"</param>
    /// <param name="year">Optional year (defaults to current year)</param>
    /// <param name="month">Optional month (defaults to current month, used for month/quarter period)</param>
    [HttpGet("analytics-summary")]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetAnalyticsSummary(
        [FromQuery] string period = "year",
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        try
        {
            // Validate period
            var validPeriods = new[] { "year", "quarter", "month", "all" };
            if (!validPeriods.Contains(period.ToLowerInvariant()))
            {
                return BadRequest($"Invalid period: {period}. Must be one of: year, quarter, month, all.");
            }

            if (year.HasValue && (year.Value < 1900 || year.Value > 3000))
            {
                return BadRequest($"Invalid year: {year}. Year must be between 1900 and 3000.");
            }

            if (month.HasValue && (month.Value < 1 || month.Value > 12))
            {
                return BadRequest($"Invalid month: {month}. Month must be between 1 and 12.");
            }

            var query = new GetAnalyticsSummaryQuery
            {
                UserId = _currentUserService.GetUserId(),
                Period = period,
                Year = year,
                Month = month
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Get upcoming bills based on recurring payment patterns
    /// </summary>
    /// <param name="daysAhead">Number of days ahead to look for upcoming bills (default: 7)</param>
    [HttpGet("upcoming-bills")]
    public async Task<ActionResult<UpcomingBillsResponse>> GetUpcomingBills([FromQuery] int daysAhead = 7)
    {
        try
        {
            // Validate daysAhead
            if (daysAhead < 1 || daysAhead > 30)
            {
                return BadRequest("daysAhead must be between 1 and 30");
            }

            var query = new GetUpcomingBillsQuery
            {
                UserId = _currentUserService.GetUserId(),
                DaysAhead = daysAhead
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while processing the request");
        }
    }
}