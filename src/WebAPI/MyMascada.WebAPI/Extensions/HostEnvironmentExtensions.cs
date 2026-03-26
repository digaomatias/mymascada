using Microsoft.Extensions.Hosting;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Extension methods for <see cref="IHostEnvironment"/> to standardize environment detection.
/// </summary>
public static class HostEnvironmentExtensions
{
    /// <summary>
    /// Returns true when the application is running in a local/non-production environment
    /// where HTTPS is not required (Development, Debug, Prod-QA).
    /// Use this instead of <see cref="HostEnvironmentEnvExtensions.IsDevelopment"/> when
    /// deciding whether to relax security constraints such as the cookie Secure flag.
    /// </summary>
    public static bool IsLocalDevelopment(this IHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || environment.EnvironmentName == "Debug"
            || environment.EnvironmentName == "Prod-QA";
    }
}
