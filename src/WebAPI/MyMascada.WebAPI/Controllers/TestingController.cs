using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MyMascada.Application.Features.Testing.Commands;
using MyMascada.Infrastructure.Data;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Controller for testing and development utilities
/// Only available in development environment
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
public class TestingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _dbContext;

    public TestingController(IMediator mediator, IWebHostEnvironment environment, ApplicationDbContext dbContext)
    {
        _mediator = mediator;
        _environment = environment;
        _dbContext = dbContext;
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

    /// <summary>
    /// Resets (deletes) a user and ALL their data for idempotent E2E test runs.
    /// Only available in development environment.
    /// </summary>
    [HttpDelete("reset-user")]
    public async Task<ActionResult<object>> ResetUser([FromQuery] string email)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { deleted = false, error = "Email query parameter is required" });
        }

        var normalizedEmail = email.ToUpperInvariant();

        // Find the user (ignore soft-delete filter)
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (user == null)
        {
            return NotFound(new { deleted = false, email, error = "User not found" });
        }

        var userId = user.Id;

        // Delete all related data in dependency order (children first)
        // Use raw SQL for efficiency and to bypass soft-delete filters

        // Financial profile & onboarding
        await _dbContext.UserFinancialProfiles.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        
        // Dashboard nudge dismissals
        await _dbContext.DashboardNudgeDismissals.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // AI-related
        await _dbContext.ChatMessages.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _dbContext.UserAiSettings.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _dbContext.AiTokenUsages.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Telegram
        await _dbContext.UserTelegramSettings.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Goals
        await _dbContext.Goals.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Budgets (budget categories cascade)
        var budgetIds = await _dbContext.Budgets.IgnoreQueryFilters().Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync();
        if (budgetIds.Count > 0)
        {
            await _dbContext.BudgetCategories.IgnoreQueryFilters().Where(x => budgetIds.Contains(x.BudgetId)).ExecuteDeleteAsync();
            await _dbContext.Budgets.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        }

        // Recurring patterns (occurrences cascade)
        var patternIds = await _dbContext.RecurringPatterns.IgnoreQueryFilters().Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync();
        if (patternIds.Count > 0)
        {
            await _dbContext.RecurringOccurrences.IgnoreQueryFilters().Where(x => patternIds.Contains(x.PatternId)).ExecuteDeleteAsync();
            await _dbContext.RecurringPatterns.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        }

        // Bank category mappings
        await _dbContext.BankCategoryMappings.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Bank connections & sync logs (need account IDs)
        var accountIds = await _dbContext.Accounts.IgnoreQueryFilters().Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync();
        if (accountIds.Count > 0)
        {
            var bankConnectionIds = await _dbContext.BankConnections.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).Select(x => x.Id).ToListAsync();
            if (bankConnectionIds.Count > 0)
            {
                await _dbContext.BankSyncLogs.IgnoreQueryFilters().Where(x => bankConnectionIds.Contains(x.BankConnectionId)).ExecuteDeleteAsync();
                await _dbContext.BankConnections.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ExecuteDeleteAsync();
            }

            // Reconciliations (items and audit logs cascade)
            var reconciliationIds = await _dbContext.Reconciliations.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).Select(x => x.Id).ToListAsync();
            if (reconciliationIds.Count > 0)
            {
                await _dbContext.ReconciliationItems.IgnoreQueryFilters().Where(x => reconciliationIds.Contains(x.ReconciliationId)).ExecuteDeleteAsync();
                await _dbContext.ReconciliationAuditLogs.IgnoreQueryFilters().Where(x => reconciliationIds.Contains(x.ReconciliationId)).ExecuteDeleteAsync();
                await _dbContext.Reconciliations.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ExecuteDeleteAsync();
            }
        }

        // Categorization candidates (depends on transactions)
        var transactionIds = accountIds.Count > 0
            ? await _dbContext.Transactions.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).Select(x => x.Id).ToListAsync()
            : new List<int>();
        
        if (transactionIds.Count > 0)
        {
            await _dbContext.CategorizationCandidates.IgnoreQueryFilters().Where(x => transactionIds.Contains(x.TransactionId)).ExecuteDeleteAsync();
        }

        // Categorization rules (conditions, applications, rule suggestion samples cascade)
        var ruleIds = await _dbContext.CategorizationRules.IgnoreQueryFilters().Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync();
        if (ruleIds.Count > 0)
        {
            await _dbContext.RuleApplications.IgnoreQueryFilters().Where(x => ruleIds.Contains(x.RuleId)).ExecuteDeleteAsync();
            await _dbContext.RuleConditions.IgnoreQueryFilters().Where(x => ruleIds.Contains(x.RuleId)).ExecuteDeleteAsync();
            await _dbContext.CategorizationRules.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        }

        // Rule suggestions
        var categoryIds = await _dbContext.Categories.IgnoreQueryFilters().Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync();
        if (categoryIds.Count > 0)
        {
            var ruleSuggestionIds = await _dbContext.RuleSuggestions.IgnoreQueryFilters().Where(x => categoryIds.Contains(x.SuggestedCategoryId)).Select(x => x.Id).ToListAsync();
            if (ruleSuggestionIds.Count > 0)
            {
                await _dbContext.RuleSuggestionSamples.IgnoreQueryFilters().Where(x => ruleSuggestionIds.Contains(x.RuleSuggestionId)).ExecuteDeleteAsync();
                await _dbContext.RuleSuggestions.IgnoreQueryFilters().Where(x => categoryIds.Contains(x.SuggestedCategoryId)).ExecuteDeleteAsync();
            }
        }

        // Transaction splits, then transactions
        if (transactionIds.Count > 0)
        {
            await _dbContext.TransactionSplits.IgnoreQueryFilters().Where(x => transactionIds.Contains(x.TransactionId)).ExecuteDeleteAsync();
        }

        // Transfers
        if (accountIds.Count > 0)
        {
            // Clear transfer references on transactions first
            await _dbContext.Transactions.IgnoreQueryFilters()
                .Where(x => accountIds.Contains(x.AccountId) && x.TransferId != null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.TransferId, (Guid?)null));
        }
        
        // Delete transfers by user
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"Transfers\" WHERE \"UserId\" = {userId}");

        // Transactions
        if (accountIds.Count > 0)
        {
            await _dbContext.Transactions.IgnoreQueryFilters().Where(x => accountIds.Contains(x.AccountId)).ExecuteDeleteAsync();
        }

        // Duplicate exclusions
        await _dbContext.DuplicateExclusions.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Account shares
        await _dbContext.AccountShares.IgnoreQueryFilters().Where(x => x.SharedByUserId == userId || x.SharedWithUserId == userId).ExecuteDeleteAsync();

        // Accounts
        await _dbContext.Accounts.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Categories
        // Sub-categories first (clear parent references)
        await _dbContext.Categories.IgnoreQueryFilters().Where(x => x.UserId == userId && x.ParentCategoryId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCategoryId, (int?)null));
        await _dbContext.Categories.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Auth tokens
        await _dbContext.RefreshTokens.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _dbContext.PasswordResetTokens.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _dbContext.EmailVerificationTokens.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Akahu credentials
        await _dbContext.AkahuUserCredentials.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Subscriptions
        await _dbContext.UserSubscriptions.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();

        // Invitation codes claimed by this user
        await _dbContext.InvitationCodes.IgnoreQueryFilters().Where(x => x.ClaimedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClaimedByUserId, (Guid?)null));

        // Finally, delete the user
        await _dbContext.Users.IgnoreQueryFilters().Where(x => x.Id == userId).ExecuteDeleteAsync();

        return Ok(new { deleted = true, email, userId });
    }
}