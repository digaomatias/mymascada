using Microsoft.EntityFrameworkCore;
using MyMascada.Infrastructure.Data;

namespace MyMascada.WebAPI.Extensions;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Use PostgreSQL in all environments
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly("MyMascada.WebAPI"));

            // Suppress model change warnings for production deployment
            if (environment.IsProduction())
            {
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            }
        });

        return services;
    }
}
