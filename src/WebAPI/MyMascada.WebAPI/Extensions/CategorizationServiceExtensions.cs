using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services;

namespace MyMascada.WebAPI.Extensions;

public static class CategorizationServiceExtensions
{
    public static IServiceCollection AddCategorizationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure categorization options
        services.Configure<CategorizationOptions>(
            configuration.GetSection(CategorizationOptions.SectionName));

        // LLM Categorization Service
        var openAiApiKey = configuration["LLM:OpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(openAiApiKey) && openAiApiKey != "YOUR_OPENAI_API_KEY")
        {
            services.AddScoped<ILlmCategorizationService, LlmCategorizationService>();
        }
        else
        {
            services.AddScoped<ILlmCategorizationService, NoOpLlmCategorizationService>();
        }

        // Categorization Pipeline Services (Phase 1: Core Pipeline)
        // Chain: Rules → BankCategory → ML → LLM
        services.AddScoped<MyMascada.Application.Features.Categorization.Handlers.RulesHandler>();
        services.AddScoped<MyMascada.Application.Features.Categorization.Handlers.BankCategoryHandler>();
        services.AddScoped<MyMascada.Application.Features.Categorization.Handlers.MLHandler>();
        services.AddScoped<MyMascada.Application.Features.Categorization.Handlers.LLMHandler>();
        services.AddScoped<MyMascada.Application.Features.Categorization.Services.CategorizationPipeline>();
        services.AddScoped<MyMascada.Application.Features.Categorization.Services.ICategorizationPipeline>(provider =>
            provider.GetRequiredService<MyMascada.Application.Features.Categorization.Services.CategorizationPipeline>());

        // Categorization Candidates Services (Phase 2: Enhanced Candidate System)
        services.AddScoped<MyMascada.Application.Features.Categorization.Services.ICategorizationCandidatesService,
            MyMascada.Application.Features.Categorization.Services.CategorizationCandidatesService>();
        services.AddScoped<MyMascada.Application.Features.Categorization.Services.ISharedCategorizationService,
            MyMascada.Application.Features.Categorization.Services.SharedCategorizationService>();

        // Rule Auto-Categorization Service
        services.AddScoped<MyMascada.Application.Features.Categorization.Services.IRuleAutoCategorizationService,
            MyMascada.Application.Features.Categorization.Services.RuleAutoCategorizationService>();

        return services;
    }
}
