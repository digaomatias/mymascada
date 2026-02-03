using System.Globalization;
using Microsoft.AspNetCore.Localization;
using MyMascada.WebAPI.Localization;

namespace MyMascada.WebAPI.Extensions;

public static class LocalizationExtensions
{
    public static readonly string[] SupportedCultures = ["en", "pt-BR"];
    public const string DefaultCulture = "en";

    public static IServiceCollection AddLocalizationServices(this IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = SupportedCultures
                .Select(c => new CultureInfo(c))
                .ToList();

            options.DefaultRequestCulture = new RequestCulture(DefaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            // Clear default providers and add our custom ones in order of priority
            options.RequestCultureProviders.Clear();

            // 1. First check authenticated user's stored locale preference
            options.RequestCultureProviders.Add(new UserLocaleRequestCultureProvider());

            // 2. Then check Accept-Language header (browser preference)
            options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

            // 3. Finally fall back to default
        });

        return services;
    }
}
