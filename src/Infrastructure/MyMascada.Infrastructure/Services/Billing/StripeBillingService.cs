using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace MyMascada.Infrastructure.Services.Billing;

public class StripeBillingService : IBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<StripeBillingService> _logger;

    public StripeBillingService(
        ApplicationDbContext context,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeBillingService> logger)
    {
        _context = context;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;

        StripeConfiguration.ApiKey = _stripeOptions.SecretKey;
    }

    public async Task<string> CreateCustomerAsync(Guid userId, string email, string name)
    {
        var service = new CustomerService();
        var customer = await service.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        });

        // Find or create user subscription with free plan
        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted);

        if (subscription == null)
        {
            var freePlan = await _context.BillingPlans
                .FirstOrDefaultAsync(p => p.StripePriceId == "free" && !p.IsDeleted);

            subscription = new UserSubscription
            {
                UserId = userId,
                PlanId = freePlan?.Id ?? 1,
                StripeCustomerId = customer.Id,
                Status = "free"
            };
            _context.UserSubscriptions.Add(subscription);
        }
        else
        {
            subscription.StripeCustomerId = customer.Id;
        }

        await _context.SaveChangesAsync();
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid userId, string priceId, string successUrl, string cancelUrl)
    {
        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted)
            ?? throw new InvalidOperationException("No subscription record found for user.");

        if (string.IsNullOrEmpty(subscription.StripeCustomerId))
            throw new InvalidOperationException("User does not have a Stripe customer ID.");

        var service = new SessionService();
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Customer = subscription.StripeCustomerId,
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        });

        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(string stripeCustomerId, string returnUrl)
    {
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl
        });

        return session.Url;
    }

    public async Task HandleCheckoutCompletedAsync(string sessionId)
    {
        var service = new SessionService();
        var session = await service.GetAsync(sessionId, new SessionGetOptions
        {
            Expand = new List<string> { "subscription" }
        });

        if (session.Subscription == null)
        {
            _logger.LogWarning("Checkout session {SessionId} has no subscription", sessionId);
            return;
        }

        var userId = session.Metadata.TryGetValue("userId", out var uid) ? uid : null;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            _logger.LogWarning("Checkout session {SessionId} has no valid userId in metadata", sessionId);
            return;
        }

        var stripeSubscription = session.Subscription;
        var firstItem = stripeSubscription.Items.Data.FirstOrDefault();
        var stripePriceId = firstItem?.Price.Id;

        var plan = await _context.BillingPlans
            .FirstOrDefaultAsync(p => p.StripePriceId == stripePriceId && !p.IsDeleted);

        if (plan == null)
        {
            _logger.LogWarning("No billing plan found for Stripe price {PriceId}", stripePriceId);
            return;
        }

        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == parsedUserId && !s.IsDeleted);

        if (subscription == null)
        {
            subscription = new UserSubscription
            {
                UserId = parsedUserId,
                StripeCustomerId = session.Customer?.Id ?? session.CustomerId
            };
            _context.UserSubscriptions.Add(subscription);
        }

        subscription.PlanId = plan.Id;
        subscription.StripeSubscriptionId = stripeSubscription.Id;
        subscription.Status = stripeSubscription.Status;
        subscription.CurrentPeriodStart = firstItem?.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = firstItem?.CurrentPeriodEnd;

        await _context.SaveChangesAsync();
        _logger.LogInformation("User {UserId} subscribed to plan {PlanName}", parsedUserId, plan.Name);
    }

    public async Task HandleSubscriptionUpdatedAsync(string subscriptionId)
    {
        var service = new SubscriptionService();
        var stripeSubscription = await service.GetAsync(subscriptionId);

        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId && !s.IsDeleted);

        if (subscription == null)
        {
            _logger.LogWarning("No local subscription found for Stripe subscription {SubscriptionId}", subscriptionId);
            return;
        }

        var firstItem = stripeSubscription.Items.Data.FirstOrDefault();
        var stripePriceId = firstItem?.Price.Id;
        var plan = await _context.BillingPlans
            .FirstOrDefaultAsync(p => p.StripePriceId == stripePriceId && !p.IsDeleted);

        if (plan != null)
        {
            subscription.PlanId = plan.Id;
        }

        subscription.Status = stripeSubscription.Status;
        subscription.CurrentPeriodStart = firstItem?.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = firstItem?.CurrentPeriodEnd;

        if (stripeSubscription.CanceledAt.HasValue)
        {
            subscription.CancelledAt = stripeSubscription.CanceledAt.Value;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Subscription {SubscriptionId} updated to status {Status}", subscriptionId, stripeSubscription.Status);
    }

    public async Task HandleSubscriptionDeletedAsync(string subscriptionId)
    {
        var subscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId && !s.IsDeleted);

        if (subscription == null)
        {
            _logger.LogWarning("No local subscription found for deleted Stripe subscription {SubscriptionId}", subscriptionId);
            return;
        }

        // Revert to free plan
        var freePlan = await _context.BillingPlans
            .FirstOrDefaultAsync(p => p.StripePriceId == "free" && !p.IsDeleted);

        subscription.PlanId = freePlan?.Id ?? 1;
        subscription.Status = "free";
        subscription.StripeSubscriptionId = null;
        subscription.CurrentPeriodStart = null;
        subscription.CurrentPeriodEnd = null;
        subscription.CancelledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Subscription {SubscriptionId} deleted, user reverted to free plan", subscriptionId);
    }

    public async Task<BillingStatusDto?> GetBillingStatusAsync(Guid userId)
    {
        var subscription = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && !s.IsDeleted);

        if (subscription == null)
            return null;

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var accountCount = await _context.Accounts
            .CountAsync(a => a.UserId == userId && !a.IsDeleted);

        var transactionCount = await _context.Transactions
            .CountAsync(t => t.Account.UserId == userId && !t.IsDeleted && t.CreatedAt >= startOfMonth);

        return new BillingStatusDto
        {
            PlanName = subscription.Plan?.Name ?? "Free",
            Status = subscription.Status,
            StripeCustomerId = subscription.StripeCustomerId,
            MaxAccounts = subscription.Plan?.MaxAccounts ?? 0,
            MaxTransactionsPerMonth = subscription.Plan?.MaxTransactionsPerMonth ?? 0,
            MaxAiCallsPerMonth = subscription.Plan?.MaxAiCallsPerMonth ?? 0,
            CurrentAccountCount = accountCount,
            CurrentMonthTransactionCount = transactionCount,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd
        };
    }
}
