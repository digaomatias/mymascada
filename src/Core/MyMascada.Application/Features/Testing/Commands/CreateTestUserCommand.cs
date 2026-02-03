using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Testing.Commands;

/// <summary>
/// Creates a test user with sample accounts and transactions for development and testing
/// </summary>
public class CreateTestUserCommand : IRequest<CreateTestUserResponse>
{
    public string Email { get; set; } = "test-user@mymascada.local";
    public string Password { get; set; } = "TestPassword123!";
    public string FirstName { get; set; } = "Test";
    public string LastName { get; set; } = "User";
    public bool CreateSampleData { get; set; } = true;
}

public class CreateTestUserResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class CreateTestUserCommandHandler : IRequestHandler<CreateTestUserCommand, CreateTestUserResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAuthenticationService _authenticationService;
    private readonly ICategorySeedingService _categorySeedingService;

    public CreateTestUserCommandHandler(
        IUserRepository userRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IAuthenticationService authenticationService,
        ICategorySeedingService categorySeedingService)
    {
        _userRepository = userRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _authenticationService = authenticationService;
        _categorySeedingService = categorySeedingService;
    }

    public async Task<CreateTestUserResponse> Handle(CreateTestUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if test user already exists
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return new CreateTestUserResponse
                {
                    IsSuccess = false,
                    Message = "Test user already exists",
                    UserId = existingUser.Id,
                    Errors = { "Test user with this email already exists" }
                };
            }

            // Create test user
            var passwordHash = await _authenticationService.HashPasswordAsync(request.Password);
            var testUser = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                NormalizedEmail = request.Email.ToUpperInvariant(),
                UserName = request.Email,
                NormalizedUserName = request.Email.ToUpperInvariant(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                PasswordHash = passwordHash,
                SecurityStamp = _authenticationService.GenerateSecurityStamp(),
                EmailConfirmed = true,
                Currency = "USD",
                TimeZone = "UTC",
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };

            await _userRepository.AddAsync(testUser);

            // Create default categories
            await _categorySeedingService.CreateDefaultCategoriesAsync(testUser.Id, cancellationToken: cancellationToken);

            if (request.CreateSampleData)
            {
                await CreateSampleAccountsAndTransactions(testUser.Id);
            }

            return new CreateTestUserResponse
            {
                IsSuccess = true,
                Message = $"Test user created successfully with {(request.CreateSampleData ? "sample data" : "no sample data")}",
                UserId = testUser.Id
            };
        }
        catch (Exception ex)
        {
            return new CreateTestUserResponse
            {
                IsSuccess = false,
                Message = "Failed to create test user",
                Errors = { ex.Message }
            };
        }
    }

    private async Task CreateSampleAccountsAndTransactions(Guid userId)
    {
        // Create sample accounts
        var checkingAccount = new Account
        {
            Name = "Test Checking Account",
            Type = AccountType.Checking,
            Institution = "Test Bank",
            LastFourDigits = "1234",
            CurrentBalance = 2500.00m,
            Currency = "USD",
            IsActive = true,
            UserId = userId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        var creditCardAccount = new Account
        {
            Name = "Test Credit Card",
            Type = AccountType.CreditCard,
            Institution = "Test Credit Union",
            LastFourDigits = "5678",
            CurrentBalance = -450.00m,
            Currency = "USD",
            IsActive = true,
            UserId = userId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        var savingsAccount = new Account
        {
            Name = "Test Savings Account",
            Type = AccountType.Savings,
            Institution = "Test Bank",
            LastFourDigits = "9012",
            CurrentBalance = 5000.00m,
            Currency = "USD",
            IsActive = true,
            UserId = userId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        await _accountRepository.AddAsync(checkingAccount);
        await _accountRepository.AddAsync(creditCardAccount);
        await _accountRepository.AddAsync(savingsAccount);

        // Create sample transactions
        var sampleTransactions = new List<Transaction>
        {
            // Income transactions (positive amounts)
            new Transaction
            {
                Amount = 3500.00m,
                TransactionDate = DateTime.Today.AddDays(-15),
                Description = "Monthly Salary",
                Type = TransactionType.Income,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Manual,
                AccountId = checkingAccount.Id,
                IsReviewed = true,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },
            
            new Transaction
            {
                Amount = 150.00m,
                TransactionDate = DateTime.Today.AddDays(-10),
                Description = "Freelance Project Payment",
                Type = TransactionType.Income,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Manual,
                AccountId = checkingAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            // Expense transactions (negative amounts)
            new Transaction
            {
                Amount = -1200.00m,
                TransactionDate = DateTime.Today.AddDays(-14),
                Description = "Monthly Rent Payment",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Manual,
                AccountId = checkingAccount.Id,
                IsReviewed = true,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -85.50m,
                TransactionDate = DateTime.Today.AddDays(-12),
                Description = "GROCERY STORE #123",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Import,
                AccountId = checkingAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -42.75m,
                TransactionDate = DateTime.Today.AddDays(-8),
                Description = "STARBUCKS COFFEE #456",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Import,
                AccountId = creditCardAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -125.00m,
                TransactionDate = DateTime.Today.AddDays(-7),
                Description = "SHELL GAS STATION",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Import,
                AccountId = creditCardAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -29.99m,
                TransactionDate = DateTime.Today.AddDays(-5),
                Description = "Netflix Subscription",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Manual,
                AccountId = creditCardAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -15.50m,
                TransactionDate = DateTime.Today.AddDays(-3),
                Description = "AMAZON MARKETPLACE",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Pending,
                Source = TransactionSource.Import,
                AccountId = creditCardAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -67.25m,
                TransactionDate = DateTime.Today.AddDays(-2),
                Description = "TARGET STORE #789",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Import,
                AccountId = checkingAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            },

            new Transaction
            {
                Amount = -95.00m,
                TransactionDate = DateTime.Today.AddDays(-1),
                Description = "ELECTRIC COMPANY BILL",
                Type = TransactionType.Expense,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Manual,
                AccountId = checkingAccount.Id,
                IsReviewed = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            }
        };

        foreach (var transaction in sampleTransactions)
        {
            await _transactionRepository.AddAsync(transaction);
        }
    }
}