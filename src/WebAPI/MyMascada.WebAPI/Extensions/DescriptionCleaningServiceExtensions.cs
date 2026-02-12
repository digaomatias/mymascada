using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.BackgroundJobs;
using MyMascada.Infrastructure.Services;

namespace MyMascada.WebAPI.Extensions;

public static class DescriptionCleaningServiceExtensions
{
    public static IServiceCollection AddDescriptionCleaningServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Description Cleaning LLM Service — always registered as scoped; returns graceful failure when no AI key is configured
        services.AddScoped<IDescriptionCleaningService, LlmDescriptionCleaningService>();

        // Background job service
        services.AddScoped<IDescriptionCleaningJobService, DescriptionCleaningJobService>();

        return services;
    }
}
