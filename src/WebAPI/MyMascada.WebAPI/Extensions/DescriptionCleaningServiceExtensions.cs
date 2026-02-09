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
        // Description Cleaning LLM Service
        var openAiApiKey = configuration["LLM:OpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(openAiApiKey) && openAiApiKey != "YOUR_OPENAI_API_KEY")
        {
            services.AddSingleton<IDescriptionCleaningService, LlmDescriptionCleaningService>();
        }
        else
        {
            services.AddSingleton<IDescriptionCleaningService, NoOpDescriptionCleaningService>();
        }

        // Background job service
        services.AddScoped<IDescriptionCleaningJobService, DescriptionCleaningJobService>();

        return services;
    }
}
