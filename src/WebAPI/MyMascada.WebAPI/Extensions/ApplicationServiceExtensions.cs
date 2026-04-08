using FluentValidation;
using MediatR;
using MyMascada.Application.Common.Behaviours;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services;
using MyMascada.Infrastructure.Services.Logging;
using MyMascada.Infrastructure.Services.UserData;

namespace MyMascada.WebAPI.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add MediatR with pipeline behaviors (order matters: first registered = outermost)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MyMascada.Application.Features.Authentication.Commands.RegisterCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehaviour<,>));
        });

        // Add FluentValidation
        services.AddValidatorsFromAssembly(typeof(MyMascada.Application.Features.Authentication.Commands.RegisterCommand).Assembly);

        // Account access service (central authorization choke point)
        services.AddScoped<IAccountAccessService, AccountAccessService>();

        // Transfer redaction service (for shared account privacy)
        services.AddScoped<ITransferRedactionService, TransferRedactionService>();

        // Transaction services
        services.AddScoped<ITransactionQueryService, MyMascada.Infrastructure.Services.TransactionQueryService>();
        services.AddScoped<MyMascada.Application.Features.Transactions.Services.TransactionReviewService>();
        services.AddScoped<MyMascada.Application.Features.Transactions.Services.TransactionDuplicateChecker>();

        // Authentication services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ITokenService, TokenService>();

        // Invite code validation service
        services.AddScoped<IInviteCodeValidationService, InviteCodeValidationService>();

        // Category services
        services.AddScoped<ICategorySeedingService, CategorySeedingService>();

        // Import services
        services.AddScoped<ICsvImportService, MyMascada.Infrastructure.Services.CsvImport.CsvImportService>();
        services.AddScoped<ICsvParserService, MyMascada.Infrastructure.Services.CsvImport.CsvParserService>();
        services.AddScoped<IAICsvAnalysisService, MyMascada.Infrastructure.Services.CsvImport.AICsvAnalysisService>();
        services.AddScoped<IImportAnalysisService, MyMascada.Application.Features.ImportReview.Services.ImportAnalysisService>();
        services.AddScoped<MyMascada.Infrastructure.Services.OfxImport.OfxParserService>();
        services.AddScoped<IOfxImportService, MyMascada.Infrastructure.Services.OfxImport.OfxImportService>();

        // Reconciliation services
        services.AddScoped<MyMascada.Application.Features.Reconciliation.Services.ITransactionMatchingService,
            MyMascada.Infrastructure.Services.Reconciliation.TransactionMatchingService>();
        services.AddScoped<MyMascada.Application.Features.Reconciliation.Services.IMatchConfidenceCalculator,
            MyMascada.Application.Features.Reconciliation.Services.MatchConfidenceCalculator>();

        // Rules services
        services.AddScoped<MyMascada.Application.Features.Rules.Services.IRuleSuggestionsService,
            MyMascada.Application.Features.Rules.Services.SimplifiedRuleSuggestionsService>();
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.IRuleSuggestionService,
            MyMascada.Application.Features.RuleSuggestions.Services.RuleSuggestionService>();

        // Rule Suggestion Analyzers
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.ICategorizationHistoryAnalyzer,
            MyMascada.Application.Features.RuleSuggestions.Services.CategorizationHistoryAnalyzer>();
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.BasicRuleSuggestionAnalyzer>();
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.AIEnhancedRuleSuggestionAnalyzer>();
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.IRuleSuggestionAnalyzerFactory,
            MyMascada.Infrastructure.Services.RuleSuggestions.RuleSuggestionAnalyzerFactory>();
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.IAIUsageTracker,
            MyMascada.Infrastructure.Services.RuleSuggestions.AIUsageTracker>();
        services.AddScoped<MyMascada.Application.Features.RuleSuggestions.Services.IPatternDetectionService,
            MyMascada.Application.Features.RuleSuggestions.Services.PatternDetectionService>();

        // OAuth state store (singleton — backed by IMemoryCache which is also singleton)
        services.AddSingleton<MyMascada.Application.Common.Interfaces.IOAuthStateStore,
            MyMascada.Infrastructure.Services.Auth.OAuthStateStore>();

        // Event Handlers
        services.AddScoped<MyMascada.Application.Events.Handlers.TransactionsCreatedEventHandler>();

        // Logging services
        services.AddScoped(typeof(IApplicationLogger<>), typeof(ApplicationLoggerAdapter<>));
        services.AddScoped<IAuditLogger, AuditLogger>();

        // User Data services (LGPD/GDPR compliance)
        services.AddScoped<IUserDataExportService, UserDataExportService>();
        services.AddScoped<IUserDataDeletionService, UserDataDeletionService>();

        // Budget services
        services.AddScoped<MyMascada.Application.Features.Budgets.Services.IBudgetCalculationService,
            MyMascada.Application.Features.Budgets.Services.BudgetCalculationService>();

        // Upcoming Bills services
        services.AddScoped<IRecurringPatternService,
            MyMascada.Application.Features.UpcomingBills.Services.RecurringPatternService>();

        // Recurring Pattern Persistence services
        services.AddScoped<MyMascada.Application.Features.RecurringPatterns.Services.IRecurringPatternPersistenceService,
            MyMascada.Application.Features.RecurringPatterns.Services.RecurringPatternPersistenceService>();

        // Notification services
        services.AddScoped<INotificationService, MyMascada.Infrastructure.Services.Notifications.NotificationService>();
        services.AddScoped<INotificationTriggerService, MyMascada.Infrastructure.Services.Notifications.NotificationTriggerService>();

        return services;
    }
}
