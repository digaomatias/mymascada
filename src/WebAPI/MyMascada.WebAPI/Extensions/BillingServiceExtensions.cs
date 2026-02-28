using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.Billing;

namespace MyMascada.WebAPI.Extensions;

public static class BillingServiceExtensions
{
    public static IServiceCollection AddBillingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));

        var stripeEnabled = configuration.GetValue<bool>("Stripe:Enabled");

        if (stripeEnabled)
        {
            services.AddScoped<IBillingService, StripeBillingService>();
        }
        else
        {
            services.AddSingleton<IBillingService, NoOpBillingService>();
        }

        return services;
    }
}
