using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFeatureFlags _featureFlags;
    private readonly StripeOptions _stripeOptions;

    public BillingController(
        IBillingService billingService,
        ICurrentUserService currentUserService,
        IFeatureFlags featureFlags,
        IOptions<StripeOptions> stripeOptions)
    {
        _billingService = billingService;
        _currentUserService = currentUserService;
        _featureFlags = featureFlags;
        _stripeOptions = stripeOptions.Value;
    }

    [HttpGet("status")]
    public async Task<ActionResult<BillingStatusResponse>> GetStatus()
    {
        if (!_featureFlags.StripeBilling)
            return NotFound();

        var userId = _currentUserService.GetUserId();
        var status = await _billingService.GetBillingStatusAsync(userId);

        if (status == null)
            return Ok(new BillingStatusResponse
            {
                PlanName = "Free",
                Status = "free",
                PublishableKey = _stripeOptions.PublishableKey
            });

        return Ok(new BillingStatusResponse
        {
            PlanName = status.PlanName,
            Status = status.Status,
            StripeCustomerId = status.StripeCustomerId,
            MaxAccounts = status.MaxAccounts,
            MaxTransactionsPerMonth = status.MaxTransactionsPerMonth,
            MaxAiCallsPerMonth = status.MaxAiCallsPerMonth,
            CurrentAccountCount = status.CurrentAccountCount,
            CurrentMonthTransactionCount = status.CurrentMonthTransactionCount,
            CurrentPeriodEnd = status.CurrentPeriodEnd,
            PublishableKey = _stripeOptions.PublishableKey
        });
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckoutSession([FromBody] CheckoutRequest request)
    {
        if (!_featureFlags.StripeBilling)
            return NotFound();

        try
        {
            var userId = _currentUserService.GetUserId();
            var successUrl = $"{request.ReturnUrl}?session_id={{CHECKOUT_SESSION_ID}}&success=true";
            var cancelUrl = $"{request.ReturnUrl}?cancelled=true";

            var url = await _billingService.CreateCheckoutSessionAsync(userId, request.PriceId, successUrl, cancelUrl);
            return Ok(new CheckoutResponse { Url = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("portal")]
    public async Task<ActionResult<PortalResponse>> CreatePortalSession([FromBody] PortalRequest request)
    {
        if (!_featureFlags.StripeBilling)
            return NotFound();

        try
        {
            var userId = _currentUserService.GetUserId();
            var status = await _billingService.GetBillingStatusAsync(userId);

            if (status?.StripeCustomerId == null)
                return BadRequest(new { message = "No Stripe customer found." });

            var url = await _billingService.CreatePortalSessionAsync(status.StripeCustomerId, request.ReturnUrl);
            return Ok(new PortalResponse { Url = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class BillingStatusResponse
{
    public string PlanName { get; set; } = "Free";
    public string Status { get; set; } = "free";
    public string? StripeCustomerId { get; set; }
    public int MaxAccounts { get; set; }
    public int MaxTransactionsPerMonth { get; set; }
    public int MaxAiCallsPerMonth { get; set; }
    public int CurrentAccountCount { get; set; }
    public int CurrentMonthTransactionCount { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public string? PublishableKey { get; set; }
}

public class CheckoutRequest
{
    public string PriceId { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
}

public class CheckoutResponse
{
    public string Url { get; set; } = string.Empty;
}

public class PortalRequest
{
    public string ReturnUrl { get; set; } = string.Empty;
}

public class PortalResponse
{
    public string Url { get; set; } = string.Empty;
}
