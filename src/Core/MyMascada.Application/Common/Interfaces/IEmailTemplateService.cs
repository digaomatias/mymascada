namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Renders email templates with data binding and localization support.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Default locale used when no locale is specified or when localized template is not found.
    /// </summary>
    const string DefaultLocale = "en-US";

    /// <summary>
    /// Renders a template by name with provided data and optional locale.
    /// </summary>
    /// <param name="templateName">Name of the template (without extension)</param>
    /// <param name="data">Data to bind to the template</param>
    /// <param name="locale">Locale code (e.g., "en-US", "pt-BR"). Falls back to default if not found.</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple of (Subject, Body) with rendered content</returns>
    Task<(string Subject, string Body)> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, object> data,
        string? locale = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets list of available template names.
    /// </summary>
    /// <returns>List of template names that can be rendered</returns>
    IReadOnlyList<string> GetAvailableTemplates();

    /// <summary>
    /// Checks if a template exists for the specified locale (or default).
    /// </summary>
    /// <param name="templateName">Name of the template (without extension)</param>
    /// <param name="locale">Optional locale code</param>
    /// <returns>True if the template exists</returns>
    bool TemplateExists(string templateName, string? locale = null);

    /// <summary>
    /// Gets list of supported locales based on available template directories.
    /// </summary>
    /// <returns>List of locale codes with available templates</returns>
    IReadOnlyList<string> GetSupportedLocales();
}
