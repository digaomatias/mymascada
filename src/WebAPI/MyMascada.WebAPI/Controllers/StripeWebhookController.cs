using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using Stripe;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly IFeatureFlags _featureFlags;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IBillingService billingService,
        IFeatureFlags featureFlags,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeWebhookController> logger)
    {
        _billingService = billingService;
        _featureFlags = featureFlags;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        if (!_featureFlags.StripeBilling)
            return NotFound();

        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _stripeOptions.WebhookSecret);

            _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

            switch (stripeEvent.Type)
            {
                case EventTypes.CheckoutSessionCompleted:
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if (session != null)
                        await _billingService.HandleCheckoutCompletedAsync(session.Id);
                    break;

                case EventTypes.CustomerSubscriptionUpdated:
                    var updatedSubscription = stripeEvent.Data.Object as Subscription;
                    if (updatedSubscription != null)
                        await _billingService.HandleSubscriptionUpdatedAsync(updatedSubscription.Id);
                    break;

                case EventTypes.CustomerSubscriptionDeleted:
                    var deletedSubscription = stripeEvent.Data.Object as Subscription;
                    if (deletedSubscription != null)
                        await _billingService.HandleSubscriptionDeletedAsync(deletedSubscription.Id);
                    break;

                default:
                    _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest();
        }
    }
}
