using Microsoft.EntityFrameworkCore;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();
    public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();
    public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();
    public DbSet<RuleApplication> RuleApplications => Set<RuleApplication>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<ReconciliationItem> ReconciliationItems => Set<ReconciliationItem>();
    public DbSet<ReconciliationAuditLog> ReconciliationAuditLogs => Set<ReconciliationAuditLog>();
    public DbSet<CategorizationCandidate> CategorizationCandidates => Set<CategorizationCandidate>();
    public DbSet<RuleSuggestion> RuleSuggestions => Set<RuleSuggestion>();
    public DbSet<RuleSuggestionSample> RuleSuggestionSamples => Set<RuleSuggestionSample>();
    public DbSet<DuplicateExclusion> DuplicateExclusions => Set<DuplicateExclusion>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<BankSyncLog> BankSyncLogs => Set<BankSyncLog>();
    public DbSet<AkahuUserCredential> AkahuUserCredentials => Set<AkahuUserCredential>();
    public DbSet<BankCategoryMapping> BankCategoryMappings => Set<BankCategoryMapping>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<RecurringPattern> RecurringPatterns => Set<RecurringPattern>();
    public DbSet<RecurringOccurrence> RecurringOccurrences => Set<RecurringOccurrence>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<InvitationCode> InvitationCodes => Set<InvitationCode>();
    public DbSet<AccountShare> AccountShares => Set<AccountShare>();
    public DbSet<UserAiSettings> UserAiSettings => Set<UserAiSettings>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UserTelegramSettings> UserTelegramSettings => Set<UserTelegramSettings>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<UserFinancialProfile> UserFinancialProfiles => Set<UserFinancialProfile>();
    public DbSet<DashboardNudgeDismissal> DashboardNudgeDismissals => Set<DashboardNudgeDismissal>();
    public DbSet<AiTokenUsage> AiTokenUsages => Set<AiTokenUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
            entity.HasIndex(e => e.NormalizedUserName).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.TimeZone).IsRequired().HasMaxLength(50);
            
            // Global query filter for soft delete
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.HasOne<User>()
                .WithMany(u => u.Accounts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Transactions)
                .WithOne(t => t.Account)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Transaction configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            
            // Auto-categorization tracking fields
            entity.Property(e => e.AutoCategorizationMethod).HasMaxLength(20);
            entity.Property(e => e.AutoCategorizationConfidence).HasPrecision(5, 4); // 0.0000 to 1.0000
            
            entity.HasOne(e => e.Transfer)
                .WithMany(t => t.Transactions)
                .HasForeignKey(e => e.TransferId)
                .HasPrincipalKey(t => t.TransferId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Index for auto-categorization queries
            entity.HasIndex(e => e.IsAutoCategorized);
            entity.HasIndex(e => e.AutoCategorizationMethod);
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasOne<User>()
                .WithMany(u => u.Categories)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ParentCategory)
                .WithMany(e => e.SubCategories)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Transactions)
                .WithOne(t => t.Category)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.CategorizationRules)
                .WithOne(r => r.Category)
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // TransactionSplit configuration
        modelBuilder.Entity<TransactionSplit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.Transaction)
                .WithMany(e => e.Splits)
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // CategorizationRule configuration
        modelBuilder.Entity<CategorizationRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Pattern).IsRequired().HasMaxLength(500);
            entity.HasOne<User>()
                .WithMany(u => u.CategorizationRules)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Conditions)
                .WithOne(c => c.Rule)
                .HasForeignKey(c => c.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Applications)
                .WithOne(a => a.Rule)
                .HasForeignKey(a => a.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RuleCondition configuration
        modelBuilder.Entity<RuleCondition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RuleApplication configuration
        modelBuilder.Entity<RuleApplication>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Transfer configuration
        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransferId).IsUnique();
            entity.Property(e => e.TransferId).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.ExchangeRate).HasPrecision(18, 8);
            entity.Property(e => e.FeeAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.SourceAccount)
                .WithMany()
                .HasForeignKey(e => e.SourceAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DestinationAccount)
                .WithMany()
                .HasForeignKey(e => e.DestinationAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RefreshToken configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedByIp).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RevokedByIp).HasMaxLength(50);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(500);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Reconciliation configuration
        modelBuilder.Entity<Reconciliation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StatementEndBalance).HasPrecision(18, 2);
            entity.Property(e => e.CalculatedBalance).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.ReconciliationItems)
                .WithOne(ri => ri.Reconciliation)
                .HasForeignKey(ri => ri.ReconciliationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.AuditLogs)
                .WithOne(al => al.Reconciliation)
                .HasForeignKey(al => al.ReconciliationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for performance
            entity.HasIndex(e => new { e.AccountId, e.Status });
            entity.HasIndex(e => e.ReconciliationDate);
            entity.HasIndex(e => e.StatementEndDate);
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ReconciliationItem configuration
        modelBuilder.Entity<ReconciliationItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchConfidence).HasPrecision(5, 4); // 0.0000 to 1.0000
            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Indexes for performance
            entity.HasIndex(e => e.ReconciliationId);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => new { e.ReconciliationId, e.ItemType });
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ReconciliationAuditLog configuration
        modelBuilder.Entity<ReconciliationAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Details).HasColumnType("jsonb");
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            
            // Indexes for performance
            entity.HasIndex(e => e.ReconciliationId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.ReconciliationId, e.Action });
            
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // CategorizationCandidate configuration
        modelBuilder.Entity<CategorizationCandidate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionId).IsRequired();
            entity.Property(e => e.CategoryId).IsRequired();
            entity.Property(e => e.CategorizationMethod).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ConfidenceScore).IsRequired().HasPrecision(5, 4); // 0.0000 to 1.0000
            entity.Property(e => e.ProcessedBy).HasMaxLength(50);
            entity.Property(e => e.Reasoning).HasColumnType("TEXT");
            entity.Property(e => e.Metadata).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue(CandidateStatus.Pending);
            entity.Property(e => e.AppliedBy).HasMaxLength(50);

            // Indexes for performance
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.TransactionId, e.Status });
            entity.HasIndex(e => e.CategorizationMethod);

            // Foreign key relationships
            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Don't delete categories if they have candidates
                
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RuleSuggestion configuration
        modelBuilder.Entity<RuleSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Pattern).IsRequired().HasMaxLength(500);
            entity.Property(e => e.GenerationMethod).HasMaxLength(100);
            entity.Property(e => e.ConfidenceScore).HasPrecision(3, 2);

            entity.HasOne(e => e.SuggestedCategory)
                .WithMany()
                .HasForeignKey(e => e.SuggestedCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedRule)
                .WithMany()
                .HasForeignKey(e => e.CreatedRuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RuleSuggestionSample configuration
        modelBuilder.Entity<RuleSuggestionSample>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.AccountName).HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);

            entity.HasOne(e => e.RuleSuggestion)
                .WithMany(rs => rs.SampleTransactions)
                .HasForeignKey(e => e.RuleSuggestionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // DuplicateExclusion configuration
        modelBuilder.Entity<DuplicateExclusion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TransactionIds).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.OriginalConfidence).HasPrecision(5, 4); // 0.0000 to 1.0000

            // Foreign key to User
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for efficient querying by user and transaction IDs
            entity.HasIndex(e => new { e.UserId, e.TransactionIds }).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // BankConnection configuration
        modelBuilder.Entity<BankConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExternalAccountId).HasMaxLength(100);
            entity.Property(e => e.ExternalAccountName).HasMaxLength(200);
            entity.Property(e => e.EncryptedSettings); // No max length - can be large encrypted JSON
            entity.Property(e => e.LastSyncError).HasMaxLength(1000);

            // Unique index on AccountId and ProviderId - one connection per provider per account
            entity.HasIndex(e => new { e.AccountId, e.ProviderId }).IsUnique();

            // Index on ExternalAccountId for lookups
            entity.HasIndex(e => e.ExternalAccountId);

            // One-to-one relationship with Account
            entity.HasOne(e => e.Account)
                .WithOne(a => a.BankConnection)
                .HasForeignKey<BankConnection>(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // BankSyncLog configuration
        modelBuilder.Entity<BankSyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Details).HasColumnType("jsonb"); // PostgreSQL jsonb type

            // Index for querying sync logs by connection and time
            entity.HasIndex(e => new { e.BankConnectionId, e.StartedAt });

            // One-to-many relationship with BankConnection
            entity.HasOne(e => e.BankConnection)
                .WithMany(bc => bc.SyncLogs)
                .HasForeignKey(e => e.BankConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // AkahuUserCredential configuration
        modelBuilder.Entity<AkahuUserCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.EncryptedAppToken).IsRequired();
            entity.Property(e => e.EncryptedUserToken).IsRequired();
            entity.Property(e => e.LastValidationError).HasMaxLength(500);

            // Unique constraint: one credential per user (user can only have one Akahu Personal App)
            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // BankCategoryMapping configuration
        modelBuilder.Entity<BankCategoryMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BankCategoryName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.CategoryId).IsRequired();
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4); // 0.0000 to 1.0000
            entity.Property(e => e.Source).HasMaxLength(20).HasDefaultValue("AI");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Unique constraint: one mapping per bank category per provider per user
            entity.HasIndex(e => new { e.NormalizedName, e.ProviderId, e.UserId }).IsUnique();

            // Index for efficient querying by user
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.ProviderId });
            entity.HasIndex(e => new { e.UserId, e.IsActive });

            // Foreign key to Category
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Don't delete mappings when category is deleted

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // PasswordResetToken configuration
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128); // SHA-256 hex = 64 chars, with buffer
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            // Index for efficient token lookup by hash
            entity.HasIndex(e => e.TokenHash).IsUnique();

            // Index for querying tokens by user
            entity.HasIndex(e => e.UserId);

            // Composite index for rate limiting queries
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });

            // Foreign key to User
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // EmailVerificationToken configuration
        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128); // SHA-256 hex = 64 chars, with buffer
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            // Index for efficient token lookup by hash
            entity.HasIndex(e => e.TokenHash).IsUnique();

            // Index for querying tokens by user
            entity.HasIndex(e => e.UserId);

            // Composite index for rate limiting queries
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });

            // Foreign key to User
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Budget configuration
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();

            // Index for efficient querying by user
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => new { e.UserId, e.StartDate });

            // One-to-many relationship with BudgetCategory
            entity.HasMany(e => e.BudgetCategories)
                .WithOne(bc => bc.Budget)
                .HasForeignKey(bc => bc.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // BudgetCategory configuration
        modelBuilder.Entity<BudgetCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BudgetId).IsRequired();
            entity.Property(e => e.CategoryId).IsRequired();
            entity.Property(e => e.BudgetedAmount).IsRequired().HasPrecision(18, 2);
            entity.Property(e => e.RolloverAmount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(500);

            // Unique constraint: one category per budget
            entity.HasIndex(e => new { e.BudgetId, e.CategoryId }).IsUnique();

            // Index for efficient querying
            entity.HasIndex(e => e.BudgetId);
            entity.HasIndex(e => e.CategoryId);

            // Foreign key to Category
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete to preserve budget history

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RecurringPattern configuration
        modelBuilder.Entity<RecurringPattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.MerchantName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NormalizedMerchantKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IntervalDays).IsRequired();
            entity.Property(e => e.AverageAmount).HasPrecision(18, 2);
            entity.Property(e => e.Confidence).HasPrecision(5, 4); // 0.0000 to 1.0000
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.NextExpectedDate).IsRequired();
            entity.Property(e => e.LastObservedAt).IsRequired();
            entity.Property(e => e.ConsecutiveMisses).HasDefaultValue(0);
            entity.Property(e => e.OccurrenceCount).HasDefaultValue(0);
            entity.Property(e => e.Notes).HasMaxLength(500);

            // Unique constraint: one pattern per normalized merchant key per user
            entity.HasIndex(e => new { e.UserId, e.NormalizedMerchantKey }).IsUnique();

            // Indexes for efficient querying
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.NextExpectedDate });
            entity.HasIndex(e => e.CategoryId);

            // Foreign key to Category
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull); // Allow category deletion

            // One-to-many relationship with occurrences
            entity.HasMany(e => e.Occurrences)
                .WithOne(o => o.Pattern)
                .HasForeignKey(o => o.PatternId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // RecurringOccurrence configuration
        modelBuilder.Entity<RecurringOccurrence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PatternId).IsRequired();
            entity.Property(e => e.ExpectedDate).IsRequired();
            entity.Property(e => e.Outcome).IsRequired();
            entity.Property(e => e.ExpectedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ActualAmount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(500);

            // Indexes for efficient querying
            entity.HasIndex(e => e.PatternId);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => new { e.PatternId, e.ExpectedDate });
            entity.HasIndex(e => new { e.PatternId, e.Outcome });

            // Foreign key to Transaction
            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.SetNull); // Allow transaction deletion

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // WaitlistEntry configuration
        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Locale).HasMaxLength(10);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // InvitationCode configuration
        modelBuilder.Entity<InvitationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NormalizedCode).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.NormalizedCode).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasOne(e => e.WaitlistEntry)
                .WithMany()
                .HasForeignKey(e => e.WaitlistEntryId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ClaimedByUser)
                .WithMany()
                .HasForeignKey(e => e.ClaimedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
        // AccountShare configuration
        modelBuilder.Entity<AccountShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvitationToken).HasMaxLength(64); // SHA-256 hex

            entity.HasOne(e => e.Account)
                .WithMany(a => a.Shares)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SharedWithUser)
                .WithMany(u => u.AccountSharesReceived)
                .HasForeignKey(e => e.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SharedByUser)
                .WithMany(u => u.AccountSharesGiven)
                .HasForeignKey(e => e.SharedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prevent duplicate active shares (same account + same user)
            entity.HasIndex(e => new { e.AccountId, e.SharedWithUserId })
                .HasFilter("\"Status\" IN (1, 2) AND \"IsDeleted\" = false")
                .IsUnique();

            // For looking up shares by recipient
            entity.HasIndex(e => e.SharedWithUserId);

            // For invitation token lookups
            entity.HasIndex(e => e.InvitationToken)
                .HasFilter("\"InvitationToken\" IS NOT NULL");

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // UserAiSettings configuration
        modelBuilder.Entity<UserAiSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(20).HasDefaultValue("general");
            entity.Property(e => e.ProviderType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModelId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ApiEndpoint).HasMaxLength(500);

            // Unique constraint: one AI settings per user per purpose
            entity.HasIndex(e => new { e.UserId, e.Purpose }).IsUnique();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired().HasColumnType("text");
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // UserTelegramSettings configuration
        modelBuilder.Entity<UserTelegramSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.EncryptedBotToken).IsRequired();
            entity.Property(e => e.WebhookSecret).IsRequired().HasMaxLength(64);
            entity.Property(e => e.BotUsername).HasMaxLength(100);

            // One bot per user
            entity.HasIndex(e => e.UserId).IsUnique();

            // O(1) webhook lookup
            entity.HasIndex(e => e.WebhookSecret).IsUnique();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Goal configuration
        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.TargetAmount).HasPrecision(18, 2);
            entity.Property(e => e.CurrentAmount).HasPrecision(18, 2);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.IsPinned).HasDefaultValue(false);

            // Indexes for efficient querying
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Status });

            // Optional FK to Account (nullable, SetNull on delete)
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.LinkedAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // UserFinancialProfile configuration
        modelBuilder.Entity<UserFinancialProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.MonthlyIncome).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyExpenses).HasPrecision(18, 2);
            entity.Property(e => e.DataEntryMethod).HasMaxLength(50);
            entity.Ignore(e => e.MonthlyAvailable);

            // One profile per user
            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // DashboardNudgeDismissal configuration
        modelBuilder.Entity<DashboardNudgeDismissal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.NudgeType).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => new { e.UserId, e.NudgeType }).IsUnique();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // AiTokenUsage configuration
        modelBuilder.Entity<AiTokenUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EstimatedCostUsd).HasPrecision(18, 8);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            entity.HasIndex(e => e.Operation);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is IAuditableEntity auditableEntity)
                    {
                        auditableEntity.CreatedAt = DateTime.UtcNow;
                        auditableEntity.UpdatedAt = DateTime.UtcNow;
                    }
                    break;
                case EntityState.Modified:
                    if (entry.Entity is IAuditableEntity modifiedEntity)
                    {
                        modifiedEntity.UpdatedAt = DateTime.UtcNow;
                    }
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}