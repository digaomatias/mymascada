using Hangfire;
using Hangfire.PostgreSql;

namespace MyMascada.WebAPI.Extensions;

public static class BackgroundJobServiceExtensions
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Hangfire Configuration
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
            }));

        // Add Hangfire Server
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount; // Use all available cores
            options.Queues = new[] { "default", "categorization" }; // Support multiple queues
        });

        // Add Hangfire Background Job Services
        services.AddScoped<MyMascada.Application.BackgroundJobs.ITransactionCategorizationJobService,
            MyMascada.Infrastructure.BackgroundJobs.TransactionCategorizationJobService>();

        // Token cleanup service
        services.AddScoped<MyMascada.Infrastructure.BackgroundJobs.ITokenCleanupService,
            MyMascada.Infrastructure.BackgroundJobs.TokenCleanupService>();

        // Recurring pattern job service
        services.AddScoped<MyMascada.Application.BackgroundJobs.IRecurringPatternJobService,
            MyMascada.Infrastructure.BackgroundJobs.RecurringPatternJobService>();

        return services;
    }
}
