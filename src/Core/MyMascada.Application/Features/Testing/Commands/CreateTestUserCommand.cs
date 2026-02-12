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
    private readonly ICategoryRepository _categoryRepository;

    public CreateTestUserCommandHandler(
        IUserRepository userRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IAuthenticationService authenticationService,
        ICategorySeedingService categorySeedingService,
        ICategoryRepository categoryRepository)
    {
        _userRepository = userRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _authenticationService = authenticationService;
        _categorySeedingService = categorySeedingService;
        _categoryRepository = categoryRepository;
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
        // Build category lookup from seeded categories
        var categories = (await _categoryRepository.GetByUserIdAsync(userId)).ToList();
        var catLookup = categories
            .Where(c => c.CanonicalKey != null)
            .ToDictionary(c => c.CanonicalKey!, c => c.Id);

        int? Cat(string key) => catLookup.TryGetValue(key, out var id) ? id : null;

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        // Create sample accounts
        var checkingAccount = new Account
        {
            Name = "Chase Checking",
            Type = AccountType.Checking,
            Institution = "Chase Bank",
            LastFourDigits = "4821",
            CurrentBalance = 3_247.63m,
            Currency = "USD",
            IsActive = true,
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var creditCardAccount = new Account
        {
            Name = "Citi Double Cash",
            Type = AccountType.CreditCard,
            Institution = "Citibank",
            LastFourDigits = "7753",
            CurrentBalance = -1_284.50m,
            Currency = "USD",
            IsActive = true,
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var savingsAccount = new Account
        {
            Name = "Ally Savings",
            Type = AccountType.Savings,
            Institution = "Ally Bank",
            LastFourDigits = "9016",
            CurrentBalance = 8_500.00m,
            Currency = "USD",
            IsActive = true,
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _accountRepository.AddAsync(checkingAccount);
        await _accountRepository.AddAsync(creditCardAccount);
        await _accountRepository.AddAsync(savingsAccount);

        // Generate 12 months of realistic transactions
        var transactions = new List<Transaction>();
        var rng = new Random(42); // Fixed seed for reproducibility
        var today = DateTime.UtcNow.Date;

        for (int monthOffset = 11; monthOffset >= 0; monthOffset--)
        {
            var monthStart = today.AddMonths(-monthOffset);
            var firstOfMonth = new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // --- MONTHLY RECURRING (checking) ---

            // Salary: 1st of month
            transactions.Add(MakeTx(
                3_500.00m, firstOfMonth.AddDays(0),
                "DIRECT DEPOSIT - ACME CORP PAYROLL",
                TransactionType.Income, TransactionSource.Import,
                checkingAccount.Id, Cat("salary"), isReviewed: true));

            // Rent: 1st of month
            transactions.Add(MakeTx(
                -1_200.00m, firstOfMonth.AddDays(0),
                "ZELLE PAYMENT - LANDLORD J. SMITH",
                TransactionType.Expense, TransactionSource.Manual,
                checkingAccount.Id, Cat("rent_mortgage"), isReviewed: true));

            // Electricity: ~15th, varies $80-120
            transactions.Add(MakeTx(
                -RoundAmount(rng, 80m, 120m), firstOfMonth.AddDays(14),
                "PACIFIC GAS & ELECTRIC",
                TransactionType.Expense, TransactionSource.Import,
                checkingAccount.Id, Cat("electricity")));

            // Internet: ~20th, fixed
            transactions.Add(MakeTx(
                -79.99m, firstOfMonth.AddDays(19),
                "COMCAST XFINITY INTERNET",
                TransactionType.Expense, TransactionSource.Import,
                checkingAccount.Id, Cat("internet_cable")));

            // --- MONTHLY RECURRING (credit card) ---

            // Netflix: ~5th
            transactions.Add(MakeTx(
                -15.99m, firstOfMonth.AddDays(4),
                "NETFLIX.COM",
                TransactionType.Expense, TransactionSource.Import,
                creditCardAccount.Id, Cat("streaming_services")));

            // Phone bill: ~12th
            transactions.Add(MakeTx(
                -65.00m, firstOfMonth.AddDays(11),
                "T-MOBILE AUTOPAY",
                TransactionType.Expense, TransactionSource.Import,
                creditCardAccount.Id, Cat("phone_bill")));

            // Gym: ~1st
            transactions.Add(MakeTx(
                -49.99m, firstOfMonth.AddDays(0),
                "PLANET FITNESS MONTHLY",
                TransactionType.Expense, TransactionSource.Import,
                creditCardAccount.Id, Cat("fitness")));

            // --- VARIABLE SPENDING ---

            // Groceries: 2-3 times per month
            var groceryCount = rng.Next(2, 4);
            string[] groceryStores = ["WHOLE FOODS MARKET", "TRADER JOE'S", "SAFEWAY #1423", "COSTCO WHOLESALE"];
            for (int i = 0; i < groceryCount; i++)
            {
                transactions.Add(MakeTx(
                    -RoundAmount(rng, 60m, 150m),
                    firstOfMonth.AddDays(rng.Next(2, 28)),
                    groceryStores[rng.Next(groceryStores.Length)],
                    TransactionType.Expense, TransactionSource.Import,
                    checkingAccount.Id, Cat("groceries")));
            }

            // Restaurants: 1-2 times per month
            var restaurantCount = rng.Next(1, 3);
            string[] restaurants = ["OLIVE GARDEN #892", "CHIPOTLE MEXICAN GRILL", "PANDA EXPRESS #1547", "OUTBACK STEAKHOUSE"];
            for (int i = 0; i < restaurantCount; i++)
            {
                transactions.Add(MakeTx(
                    -RoundAmount(rng, 25m, 80m),
                    firstOfMonth.AddDays(rng.Next(1, 28)),
                    restaurants[rng.Next(restaurants.Length)],
                    TransactionType.Expense, TransactionSource.Import,
                    creditCardAccount.Id, Cat("restaurants")));
            }

            // Gas: 2 times per month
            string[] gasStations = ["SHELL OIL", "CHEVRON #4521", "COSTCO GAS", "BP AMOCO"];
            for (int i = 0; i < 2; i++)
            {
                transactions.Add(MakeTx(
                    -RoundAmount(rng, 40m, 70m),
                    firstOfMonth.AddDays(rng.Next(3, 27)),
                    gasStations[rng.Next(gasStations.Length)],
                    TransactionType.Expense, TransactionSource.Import,
                    checkingAccount.Id, Cat("gas_fuel")));
            }

            // Coffee: 2-3 times per month
            var coffeeCount = rng.Next(2, 4);
            string[] coffeeShops = ["STARBUCKS #13842", "DUTCH BROS COFFEE", "PEET'S COFFEE & TEA", "BLUE BOTTLE COFFEE"];
            for (int i = 0; i < coffeeCount; i++)
            {
                transactions.Add(MakeTx(
                    -RoundAmount(rng, 5m, 12m),
                    firstOfMonth.AddDays(rng.Next(1, 28)),
                    coffeeShops[rng.Next(coffeeShops.Length)],
                    TransactionType.Expense, TransactionSource.Import,
                    creditCardAccount.Id, Cat("coffee_shops")));
            }

            // Amazon/shopping: 1 time per month
            transactions.Add(MakeTx(
                -RoundAmount(rng, 15m, 100m),
                firstOfMonth.AddDays(rng.Next(5, 25)),
                "AMAZON.COM*" + rng.Next(100, 999) + "A",
                TransactionType.Expense, TransactionSource.Import,
                creditCardAccount.Id, Cat("electronics")));

            // Freelance income: every 2-3 months
            if (monthOffset % 3 == 1)
            {
                transactions.Add(MakeTx(
                    RoundAmount(rng, 200m, 500m),
                    firstOfMonth.AddDays(rng.Next(10, 25)),
                    "PAYPAL TRANSFER - FREELANCE PROJECT",
                    TransactionType.Income, TransactionSource.Import,
                    checkingAccount.Id, Cat("freelance")));
            }
        }

        foreach (var transaction in transactions)
        {
            await _transactionRepository.AddAsync(transaction);
        }
    }

    private static Transaction MakeTx(
        decimal amount, DateTime date, string description,
        TransactionType type, TransactionSource source,
        int accountId, int? categoryId, bool isReviewed = false)
    {
        var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var utcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        return new Transaction
        {
            Amount = amount,
            TransactionDate = utcDate,
            Description = description,
            Type = type,
            Status = TransactionStatus.Cleared,
            Source = source,
            AccountId = accountId,
            CategoryId = categoryId,
            IsReviewed = isReviewed,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static decimal RoundAmount(Random rng, decimal min, decimal max)
    {
        var range = (double)(max - min);
        return Math.Round(min + (decimal)(rng.NextDouble() * range), 2);
    }
}