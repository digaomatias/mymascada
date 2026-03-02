using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Billing;

public class NoOpBillingService : IBillingService
{
    public Task<string> CreateCustomerAsync(Guid userId, string email, string name)
        => Task.FromResult(string.Empty);

    public Task<string> CreateCheckoutSessionAsync(Guid userId, string priceId, string successUrl, string cancelUrl)
        => throw new InvalidOperationException("Billing is not enabled.");

    public Task<string> CreatePortalSessionAsync(string stripeCustomerId, string returnUrl)
        => throw new InvalidOperationException("Billing is not enabled.");

    public Task HandleCheckoutCompletedAsync(string sessionId)
        => Task.CompletedTask;

    public Task HandleSubscriptionUpdatedAsync(string subscriptionId)
        => Task.CompletedTask;

    public Task HandleSubscriptionDeletedAsync(string subscriptionId)
        => Task.CompletedTask;

    public Task<BillingStatusDto?> GetBillingStatusAsync(Guid userId)
        => Task.FromResult<BillingStatusDto?>(null);
}
