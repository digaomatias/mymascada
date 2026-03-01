using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.AI;

namespace MyMascada.WebAPI.Extensions;

public static class AiChatServiceExtensions
{
    public static IServiceCollection AddAiChatServices(this IServiceCollection services)
    {
        services.AddScoped<IFinancialContextBuilder, FinancialContextBuilder>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAiTokenTracker, AiTokenTracker>();
        return services;
    }
}
