namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Billing service interface for managing Stripe subscriptions and customer lifecycle.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Creates a Stripe customer for a user.
    /// </summary>
    /// <param name="userId">The internal user ID</param>
    /// <param name="email">The user's email address</param>
    /// <param name="name">The user's display name</param>
    /// <returns>The Stripe customer ID</returns>
    Task<string> CreateCustomerAsync(Guid userId, string email, string name);

    /// <summary>
    /// Creates a Stripe Checkout session for subscribing to a plan.
    /// </summary>
    /// <param name="userId">The internal user ID</param>
    /// <param name="priceId">The Stripe price ID for the plan</param>
    /// <param name="successUrl">URL to redirect to on successful checkout</param>
    /// <param name="cancelUrl">URL to redirect to on cancelled checkout</param>
    /// <returns>The Checkout session URL</returns>
    Task<string> CreateCheckoutSessionAsync(Guid userId, string priceId, string successUrl, string cancelUrl);

    /// <summary>
    /// Creates a Stripe Customer Portal session for managing subscriptions.
    /// </summary>
    /// <param name="stripeCustomerId">The Stripe customer ID</param>
    /// <param name="returnUrl">URL to redirect to when the user exits the portal</param>
    /// <returns>The portal session URL</returns>
    Task<string> CreatePortalSessionAsync(string stripeCustomerId, string returnUrl);

    /// <summary>
    /// Handles the checkout.session.completed webhook event.
    /// </summary>
    /// <param name="sessionId">The Stripe Checkout session ID</param>
    Task HandleCheckoutCompletedAsync(string sessionId);

    /// <summary>
    /// Handles the customer.subscription.updated webhook event.
    /// </summary>
    /// <param name="subscriptionId">The Stripe subscription ID</param>
    Task HandleSubscriptionUpdatedAsync(string subscriptionId);

    /// <summary>
    /// Handles the customer.subscription.deleted webhook event.
    /// </summary>
    /// <param name="subscriptionId">The Stripe subscription ID</param>
    Task HandleSubscriptionDeletedAsync(string subscriptionId);

    /// <summary>
    /// Gets the current billing status for a user.
    /// </summary>
    /// <param name="userId">The internal user ID</param>
    /// <returns>The billing status, or null if the user has no billing record</returns>
    Task<BillingStatusDto?> GetBillingStatusAsync(Guid userId);
}

/// <summary>
/// DTO representing the current billing status and plan limits for a user.
/// </summary>
public class BillingStatusDto
{
    /// <summary>
    /// Display name of the current plan (e.g., "Free", "Pro")
    /// </summary>
    public string PlanName { get; set; } = "Free";

    /// <summary>
    /// Subscription status (e.g., "free", "active", "canceled", "past_due")
    /// </summary>
    public string Status { get; set; } = "free";

    /// <summary>
    /// The Stripe customer ID, if the user has been linked to Stripe
    /// </summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// Maximum number of accounts allowed on the current plan
    /// </summary>
    public int MaxAccounts { get; set; }

    /// <summary>
    /// Maximum number of transactions allowed per month on the current plan
    /// </summary>
    public int MaxTransactionsPerMonth { get; set; }

    /// <summary>
    /// Maximum number of AI categorization calls allowed per month on the current plan
    /// </summary>
    public int MaxAiCallsPerMonth { get; set; }

    /// <summary>
    /// The user's current number of accounts
    /// </summary>
    public int CurrentAccountCount { get; set; }

    /// <summary>
    /// The user's transaction count for the current month
    /// </summary>
    public int CurrentMonthTransactionCount { get; set; }

    /// <summary>
    /// End date of the current billing period, if on a paid plan
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; set; }
}
