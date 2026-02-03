using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MyMascada.Infrastructure.Data;
using MyMascada.WebAPI.Extensions;

namespace MyMascada.WebAPI.Localization;

/// <summary>
/// Custom culture provider that reads the user's stored locale preference from the database.
/// Falls through to the next provider if user is not authenticated or locale is not set.
/// </summary>
public class UserLocaleRequestCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // Check if user is authenticated
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return null; // Fall through to next provider
        }

        // Get user ID from claims
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null; // Fall through to next provider
        }

        try
        {
            // Get the database context from DI
            var dbContext = httpContext.RequestServices.GetService<ApplicationDbContext>();
            if (dbContext == null)
            {
                return null;
            }

            // Fetch user's locale preference
            var userLocale = await dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Locale)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userLocale))
            {
                return null; // Fall through to next provider
            }

            // Normalize locale to our supported formats
            var normalizedLocale = NormalizeLocale(userLocale);

            // Validate it's a supported culture
            if (!LocalizationExtensions.SupportedCultures.Contains(normalizedLocale, StringComparer.OrdinalIgnoreCase))
            {
                return null; // Fall through to next provider
            }

            return new ProviderCultureResult(normalizedLocale);
        }
        catch (Exception)
        {
            // Log error but don't fail the request - fall through to next provider
            return null;
        }
    }

    /// <summary>
    /// Normalizes locale strings to our supported format.
    /// Examples: "en-US" -> "en", "pt-BR" -> "pt-BR", "en" -> "en"
    /// </summary>
    private static string NormalizeLocale(string locale)
    {
        if (string.IsNullOrEmpty(locale))
            return LocalizationExtensions.DefaultCulture;

        // Handle exact matches first
        if (LocalizationExtensions.SupportedCultures.Contains(locale, StringComparer.OrdinalIgnoreCase))
            return locale;

        // Handle "en-US", "en-GB", etc. -> "en"
        if (locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en";

        // Handle "pt", "pt-PT" -> "pt-BR" (default Portuguese to Brazilian)
        if (locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";

        return LocalizationExtensions.DefaultCulture;
    }
}
