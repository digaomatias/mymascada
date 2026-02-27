using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MyMascada.Infrastructure.Data;
using MyMascada.WebAPI.Extensions;
using MyMascada.WebAPI.Middleware;
using MyMascada.WebAPI.Services;
using Serilog;
using Serilog.Events;
using Hangfire;

// Configure Serilog early to capture startup logs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MyMascada")
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting MyMascada Finance Application");

    var builder = WebApplication.CreateBuilder(args);

    // Register PII masking service early (needed for Serilog configuration)
    var piiMaskingService = new MyMascada.Infrastructure.Services.Logging.PiiMaskingService();
    builder.Services.AddSingleton<MyMascada.Infrastructure.Services.Logging.IPiiMaskingService>(piiMaskingService);

    // Configure Serilog from configuration with PII masking
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "MyMascada")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.With(new MyMascada.Infrastructure.Services.Logging.PiiMaskingEnricher(piiMaskingService)));

// Configure forwarded headers for reverse proxy (Nginx Proxy Manager)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    // Trust only the Docker bridge network range (172.16.0.0/12) where the
    // internal reverse proxy lives. This prevents X-Forwarded-* header spoofing
    // from external clients.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
        System.Net.IPAddress.Parse("172.16.0.0"), 12));

    // Limit the number of proxy hops to prevent header spoofing
    options.ForwardLimit = 2;
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configure Swagger to use string values for enums
    options.UseInlineDefinitionsForEnums();
    options.CustomSchemaIds(type => type.FullName);
});

// Add HTTP context accessor and current user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MyMascada.Application.Common.Interfaces.ICurrentUserService, MyMascada.WebAPI.Services.CurrentUserService>();

// Add Data Protection and Session support for OAuth
// Persist keys to a directory so they survive container restarts
var isLocalDev = builder.Environment.IsDevelopment()
    || builder.Environment.EnvironmentName == "Debug"
    || builder.Environment.EnvironmentName == "Prod-QA";
var keysDirectory = isLocalDev
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMascada", "Keys")
    : "/app/data/keys";

Directory.CreateDirectory(keysDirectory);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("MyMascada");

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// Configure cookie policy for OAuth
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false; // Disable for development
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

// Configure beta access options
builder.Services.Configure<MyMascada.Application.Common.Configuration.BetaAccessOptions>(
    builder.Configuration.GetSection(MyMascada.Application.Common.Configuration.BetaAccessOptions.SectionName));

// Configure core application options (frontend URL, etc.)
builder.Services.Configure<MyMascada.Application.Common.Configuration.AppOptions>(
    builder.Configuration.GetSection(MyMascada.Application.Common.Configuration.AppOptions.SectionName));

// Add organized service groups using extension methods
builder.Services.AddFeatureFlags(builder.Configuration); // Must be first — other extensions depend on IFeatureFlags
builder.Services.AddDatabaseServices(builder.Configuration, builder.Environment);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();
builder.Services.AddCategorizationServices(builder.Configuration);
builder.Services.AddDescriptionCleaningServices(builder.Configuration);
builder.Services.AddBankProviderServices(builder.Configuration);
builder.Services.AddEmailServices(builder.Configuration);
builder.Services.AddHealthCheckServices();
builder.Services.AddBackgroundJobs(builder.Configuration);
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddRateLimitingConfiguration();
builder.Services.AddAiChatServices();
builder.Services.AddTelegramServices();

// Add localization services for multi-language support
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddLocalizationServices();

// Hard-fail if JWT key is still the placeholder in Production
if (!builder.Environment.IsDevelopment())
{
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
    if (jwtKey.Contains("CHANGE_ME") || jwtKey.Contains("DevOnly"))
    {
        throw new InvalidOperationException(
            "JWT signing key has not been configured. " +
            "Set the Jwt__Key environment variable to a secure, random value (>= 256 bits).");
    }
}

var app = builder.Build();

// Log startup feature status banner
{
    var dbStatus = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection"))
        ? "Connected"
        : "Not Configured";

    var aiKey = builder.Configuration["LLM:OpenAI:ApiKey"];
    var aiStatus = !string.IsNullOrWhiteSpace(aiKey) && !string.Equals(aiKey, "YOUR_OPENAI_API_KEY", StringComparison.Ordinal)
        ? "Enabled"
        : "Disabled (no API key)";

    var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    var googleStatus = !string.IsNullOrWhiteSpace(googleClientId) && !string.Equals(googleClientId, "YOUR_GOOGLE_CLIENT_ID", StringComparison.Ordinal)
        ? "Enabled"
        : "Disabled (no credentials)";

    var akahuEnabled = builder.Configuration.GetValue<bool>("Akahu:Enabled");
    var bankSyncStatus = akahuEnabled ? "Enabled" : "Disabled";

    var emailEnabled = string.Equals(builder.Configuration["Email:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    var emailStatus = emailEnabled ? "Enabled" : "Disabled";

    app.Logger.LogInformation("============================================");
    app.Logger.LogInformation("MyMascada Finance Application");
    app.Logger.LogInformation("============================================");
    app.Logger.LogInformation("  Database:           {DatabaseStatus}", dbStatus);
    app.Logger.LogInformation("  AI Categorization:  {AiStatus}", aiStatus);
    app.Logger.LogInformation("  Google OAuth:       {GoogleStatus}", googleStatus);
    app.Logger.LogInformation("  Bank Sync (Akahu):  {BankSyncStatus}", bankSyncStatus);
    app.Logger.LogInformation("  Email:              {EmailStatus}", emailStatus);
    app.Logger.LogInformation("============================================");
}

// Add request logging with CORS debugging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode > 499
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode > 399
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
    
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
        diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
        diagnosticContext.Set("Origin", httpContext.Request.Headers.Origin.FirstOrDefault());
        diagnosticContext.Set("Referer", httpContext.Request.Headers.Referer.FirstOrDefault());
        
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId", httpContext.User.Identity.Name);
        }
    };
});

// Only auto-migrate in development - production migrations should be done via pipeline
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        try
        {
            // Check if database exists, if not log but don't fail
            if (context.Database.CanConnect())
            {
                // Only apply pending migrations if database exists and we can connect
                var pendingMigrations = context.Database.GetPendingMigrations();
                if (pendingMigrations.Any())
                {
                    app.Logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                        pendingMigrations.Count(), 
                        string.Join(", ", pendingMigrations));
                    context.Database.Migrate();
                    app.Logger.LogInformation("Development database migrations applied successfully");
                }
                else
                {
                    app.Logger.LogInformation("No pending migrations to apply");
                }
            }
            else
            {
                app.Logger.LogWarning("Cannot connect to database. Please ensure the database exists and is accessible.");
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to apply development migrations");
            // Continue startup even if migrations fail in development
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use forwarded headers (must be first, before any middleware that needs Request.Scheme/Host)
app.UseForwardedHeaders();

// Add security headers (early in pipeline, before any response is sent)
app.UseSecurityHeaders();

// Add request localization (determines culture for each request)
app.UseRequestLocalization();

// Add global exception handling middleware (early in pipeline)
app.UseGlobalExceptionHandling();

// CORS debugging middleware — development only
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        var method = context.Request.Method;
        var path = context.Request.Path;

        logger.LogInformation("CORS Debug: {Method} {Path} from Origin: {Origin}", method, path, origin ?? "null");

        await next();

        var corsHeaders = string.Join(", ", context.Response.Headers
            .Where(h => h.Key.StartsWith("Access-Control"))
            .Select(h => $"{h.Key}={h.Value}"));

        logger.LogInformation("CORS Debug: Response headers: {Headers}", corsHeaders);
    });
}

// Enable CORS (must be before auth)
app.UseCors("AllowFrontend");

// Add cookie policy (must be before session and auth)
app.UseCookiePolicy();

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

// Add rate limiting (after auth so we can identify users)
app.UseRateLimiter();

// Add Hangfire Dashboard (only in development for security)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
    app.Logger.LogInformation("Hangfire Dashboard available at /hangfire");
}

// Register recurring Hangfire jobs (use DI-based API instead of static RecurringJob)
var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

recurringJobManager.AddOrUpdate<MyMascada.Infrastructure.BackgroundJobs.ITokenCleanupService>(
    "cleanup-expired-refresh-tokens",
    service => service.CleanupExpiredRefreshTokensAsync(),
    Hangfire.Cron.Daily(3, 0)); // Run daily at 3:00 AM

recurringJobManager.AddOrUpdate<MyMascada.Infrastructure.BackgroundJobs.ITokenCleanupService>(
    "cleanup-expired-password-reset-tokens",
    service => service.CleanupExpiredPasswordResetTokensAsync(),
    Hangfire.Cron.Daily(3, 15)); // Run daily at 3:15 AM

recurringJobManager.AddOrUpdate<MyMascada.Application.BackgroundJobs.IRecurringPatternJobService>(
    "daily-recurring-pattern-detection",
    service => service.ProcessAllUsersAsync(null),
    Hangfire.Cron.Daily(2, 0)); // Run daily at 2:00 AM

// Map controllers
app.MapControllers();

// Map health check endpoint for load balancers and container orchestration
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Add a simple test endpoint
app.MapGet("/", () => "MyMascada API is running!");

    app.Run();
    
    Log.Information("MyMascada Finance Application stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "MyMascada Finance Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for integration tests
public partial class Program { }
