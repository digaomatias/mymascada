using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using Scriban;
using Scriban.Runtime;

namespace MyMascada.Infrastructure.Services.Email.Templates;

/// <summary>
/// File-based template service using Scriban for templating.
/// Templates are loaded from the configured TemplateDirectory with locale support.
/// Directory structure: {TemplateDirectory}/{locale}/{templateName}.{subject.txt|body.html}
/// Falls back to default locale (en-US) if localized template is not found.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly EmailOptions _options;
    private readonly IApplicationLogger<EmailTemplateService> _logger;
    private readonly string _templatePath;
    private readonly ConcurrentDictionary<string, Template> _templateCache = new();

    public EmailTemplateService(
        IOptions<EmailOptions> options,
        IWebHostEnvironment env,
        IApplicationLogger<EmailTemplateService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _templatePath = Path.Combine(env.ContentRootPath, _options.TemplateDirectory);

        _logger.LogInformation("EmailTemplateService initialized with template path: {Path}", _templatePath);
    }

    /// <inheritdoc />
    public async Task<(string Subject, string Body)> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, object> data,
        string? locale = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        // Normalize locale or use default
        var effectiveLocale = NormalizeLocale(locale);

        // Try to load templates with locale fallback
        var subjectTemplate = await GetOrLoadTemplateWithFallbackAsync(templateName, "subject.txt", effectiveLocale, ct);
        var bodyTemplate = await GetOrLoadTemplateWithFallbackAsync(templateName, "body.html", effectiveLocale, ct);

        var context = CreateTemplateContext(data);

        var subject = await subjectTemplate.RenderAsync(context);
        var body = await bodyTemplate.RenderAsync(context);

        return (subject.Trim(), body);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableTemplates()
    {
        var templates = new HashSet<string>();

        // Check for locale subdirectories
        if (Directory.Exists(_templatePath))
        {
            foreach (var localeDir in Directory.GetDirectories(_templatePath))
            {
                var bodyFiles = Directory.GetFiles(localeDir, "*.body.html");
                foreach (var file in bodyFiles)
                {
                    var templateName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                    templates.Add(templateName);
                }
            }

            // Also check root directory for backward compatibility
            var rootBodyFiles = Directory.GetFiles(_templatePath, "*.body.html");
            foreach (var file in rootBodyFiles)
            {
                var templateName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
                templates.Add(templateName);
            }
        }
        else
        {
            _logger.LogWarning("Template directory does not exist: {Path}", _templatePath);
        }

        return templates.OrderBy(x => x).ToList();
    }

    /// <inheritdoc />
    public bool TemplateExists(string templateName, string? locale = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return false;

        var effectiveLocale = NormalizeLocale(locale);

        // Check locale-specific path first
        var localePath = Path.Combine(_templatePath, effectiveLocale);
        if (Directory.Exists(localePath))
        {
            var subjectPath = Path.Combine(localePath, $"{templateName}.subject.txt");
            var bodyPath = Path.Combine(localePath, $"{templateName}.body.html");
            if (File.Exists(subjectPath) && File.Exists(bodyPath))
                return true;
        }

        // Check default locale if different
        if (effectiveLocale != IEmailTemplateService.DefaultLocale)
        {
            var defaultLocalePath = Path.Combine(_templatePath, IEmailTemplateService.DefaultLocale);
            if (Directory.Exists(defaultLocalePath))
            {
                var subjectPath = Path.Combine(defaultLocalePath, $"{templateName}.subject.txt");
                var bodyPath = Path.Combine(defaultLocalePath, $"{templateName}.body.html");
                if (File.Exists(subjectPath) && File.Exists(bodyPath))
                    return true;
            }
        }

        // Check root directory for backward compatibility
        var rootSubjectPath = Path.Combine(_templatePath, $"{templateName}.subject.txt");
        var rootBodyPath = Path.Combine(_templatePath, $"{templateName}.body.html");
        return File.Exists(rootSubjectPath) && File.Exists(rootBodyPath);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSupportedLocales()
    {
        var locales = new List<string>();

        if (Directory.Exists(_templatePath))
        {
            foreach (var dir in Directory.GetDirectories(_templatePath))
            {
                var localeName = Path.GetFileName(dir);
                // Check if it looks like a locale (e.g., "en-US", "pt-BR")
                if (localeName.Length >= 2 && localeName.Contains('-'))
                {
                    locales.Add(localeName);
                }
            }
        }

        // Ensure default locale is always included
        if (!locales.Contains(IEmailTemplateService.DefaultLocale))
        {
            locales.Insert(0, IEmailTemplateService.DefaultLocale);
        }

        return locales.OrderBy(x => x).ToList();
    }

    private async Task<Template> GetOrLoadTemplateWithFallbackAsync(
        string templateName,
        string extension,
        string locale,
        CancellationToken ct)
    {
        var fileName = $"{templateName}.{extension}";

        // Try locale-specific path first
        var localeFilePath = Path.Combine(_templatePath, locale, fileName);
        var cacheKey = $"{locale}/{fileName}";

        if (_templateCache.TryGetValue(cacheKey, out var cached))
            return cached;

        if (File.Exists(localeFilePath))
        {
            return await LoadAndCacheTemplateAsync(localeFilePath, cacheKey, ct);
        }

        // Fall back to default locale if different
        if (locale != IEmailTemplateService.DefaultLocale)
        {
            var defaultLocaleFilePath = Path.Combine(_templatePath, IEmailTemplateService.DefaultLocale, fileName);
            var defaultCacheKey = $"{IEmailTemplateService.DefaultLocale}/{fileName}";

            if (_templateCache.TryGetValue(defaultCacheKey, out cached))
                return cached;

            if (File.Exists(defaultLocaleFilePath))
            {
                _logger.LogDebug("Template not found for locale {Locale}, falling back to {DefaultLocale}: {Template}",
                    locale, IEmailTemplateService.DefaultLocale, fileName);
                return await LoadAndCacheTemplateAsync(defaultLocaleFilePath, defaultCacheKey, ct);
            }
        }

        // Fall back to root directory for backward compatibility
        var rootFilePath = Path.Combine(_templatePath, fileName);
        var rootCacheKey = $"root/{fileName}";

        if (_templateCache.TryGetValue(rootCacheKey, out cached))
            return cached;

        if (File.Exists(rootFilePath))
        {
            _logger.LogDebug("Template not found in locale directories, using root: {Template}", fileName);
            return await LoadAndCacheTemplateAsync(rootFilePath, rootCacheKey, ct);
        }

        // Template not found anywhere
        _logger.LogError(null, "Email template not found: {FileName} (Locale: {Locale}, Path: {Path})",
            new { FileName = fileName, Locale = locale, Path = _templatePath });
        throw new FileNotFoundException($"Email template not found: {fileName}", localeFilePath);
    }

    private async Task<Template> LoadAndCacheTemplateAsync(string filePath, string cacheKey, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var template = Template.Parse(content, filePath);

        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            _logger.LogError(null, "Template parse error in {FilePath}: {Errors}",
                new { FilePath = filePath, Errors = errors });
            throw new InvalidOperationException($"Template parse error in {filePath}: {errors}");
        }

        _templateCache.TryAdd(cacheKey, template);
        _logger.LogDebug("Template loaded and cached: {CacheKey}", cacheKey);

        return template;
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return IEmailTemplateService.DefaultLocale;

        // Handle common variations (e.g., "pt-br" -> "pt-BR", "en" -> "en-US")
        var parts = locale.Split('-');
        if (parts.Length == 1)
        {
            // Language only - map to common locale
            return parts[0].ToLowerInvariant() switch
            {
                "en" => "en-US",
                "pt" => "pt-BR",
                _ => $"{parts[0].ToLowerInvariant()}-{parts[0].ToUpperInvariant()}"
            };
        }

        // Normalize format: lowercase language, uppercase region
        return $"{parts[0].ToLowerInvariant()}-{parts[1].ToUpperInvariant()}";
    }

    private static TemplateContext CreateTemplateContext(IReadOnlyDictionary<string, object> data)
    {
        var scriptObject = new ScriptObject();

        foreach (var kvp in data)
        {
            scriptObject.Add(kvp.Key, kvp.Value);
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return context;
    }
}
