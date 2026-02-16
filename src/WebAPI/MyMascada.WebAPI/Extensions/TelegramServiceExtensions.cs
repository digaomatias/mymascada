using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Repositories;
using MyMascada.Infrastructure.Services.Telegram;

namespace MyMascada.WebAPI.Extensions;

public static class TelegramServiceExtensions
{
    public static IServiceCollection AddTelegramServices(this IServiceCollection services)
    {
        services.AddScoped<IUserTelegramSettingsRepository, UserTelegramSettingsRepository>();
        services.AddHttpClient<ITelegramBotService, TelegramBotService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
