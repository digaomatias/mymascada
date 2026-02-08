using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Repositories;

namespace MyMascada.WebAPI.Extensions;

public static class RepositoryServiceExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Core repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();

        // Reconciliation repositories
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IReconciliationItemRepository, ReconciliationItemRepository>();
        services.AddScoped<IReconciliationAuditLogRepository, ReconciliationAuditLogRepository>();

        // Rules repositories
        services.AddScoped<ICategorizationRuleRepository, CategorizationRuleRepository>();
        services.AddScoped<IRuleSuggestionRepository, RuleSuggestionRepository>();

        // Categorization repositories
        services.AddScoped<ICategorizationCandidatesRepository, CategorizationCandidatesRepository>();
        services.AddScoped<IDuplicateExclusionRepository, DuplicateExclusionRepository>();

        // Budget repositories
        services.AddScoped<IBudgetRepository, BudgetRepository>();

        // Recurring pattern repositories
        services.AddScoped<IRecurringPatternRepository, RecurringPatternRepository>();

        // Waitlist repositories
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<IInvitationCodeRepository, InvitationCodeRepository>();

        // Account sharing repositories
        services.AddScoped<IAccountShareRepository, AccountShareRepository>();

        return services;
    }
}
