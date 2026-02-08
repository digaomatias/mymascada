namespace MyMascada.WebAPI.Extensions;

public static class CorsServiceExtensions
{
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var environment = services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                var origins = new List<string>();

                // Only include localhost origins in Development
                if (environment.IsDevelopment())
                {
                    origins.AddRange(new[]
                    {
                        "http://localhost:3000",
                        "http://localhost:3001",
                        "http://localhost:3003",
                        "https://localhost:5126"
                    });
                }

                // Read configured origins (used in all environments)
                var configuredOrigins = configuration["Cors:AllowedOrigins"]?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? Array.Empty<string>();

                origins.AddRange(configuredOrigins);

                var allOrigins = origins
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                policy.WithOrigins(allOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
                      .WithExposedHeaders("Authorization", "Content-Type", "Accept", "Origin", "Access-Control-Allow-Origin");
            });
        });

        return services;
    }
}
