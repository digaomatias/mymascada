using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Accounts.Queries;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;
using MyMascada.Infrastructure.Services;
using MyMascada.WebAPI.Controllers;
using MyMascada.WebAPI.Extensions;
using MyMascada.WebAPI.Services;
using Xunit;

namespace MyMascada.Tests.Integration;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private const string Issuer = "mymascada-accounts-test";
    private const string Audience = "mymascada-accounts-test-api";
    private readonly string _signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private WebApplication _app = null!;
    private AsyncServiceScope _scope;
    protected HttpClient Client = null!;
    protected ApplicationDbContext DbContext = null!;
    protected readonly Guid TestUserId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = _signingKey,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience
        });

        var databaseName = Guid.NewGuid().ToString("N");
        var services = builder.Services;
        services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddMvc();
        services.AddJwtAuthentication(builder.Configuration);
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAccountAccessService, AccountAccessService>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountShareRepository, AccountShareRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITransactionQueryService, TransactionQueryService>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAccountDetailsQuery).Assembly));

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        Client = _app.GetTestClient();
        _scope = _app.Services.CreateAsyncScope();
        DbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DbContext.Database.EnsureCreatedAsync();
        SetupAuthentication();
    }

    private void SetupAuthentication()
    {
        // Create a JWT token for testing
        var token = GenerateJwtToken(TestUserId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private string GenerateJwtToken(Guid userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_signingKey);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Email, "test@example.com")
            }),
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    protected async Task<User> CreateTestUserAsync()
    {
        var user = new User
        {
            Id = TestUserId,
            Email = "test@example.com",
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();
        return user;
    }

    protected async Task<Account> CreateTestAccountAsync(string name = "Test Account", decimal initialBalance = 1000m)
    {
        var account = new Account
        {
            Name = name,
            Type = Domain.Enums.AccountType.Checking,
            CurrentBalance = initialBalance,
            Currency = "USD",
            UserId = TestUserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Accounts.Add(account);
        await DbContext.SaveChangesAsync();
        return account;
    }

    protected async Task<Transaction> CreateTestTransactionAsync(int accountId, decimal amount = 100m, string description = "Test Transaction")
    {
        var transaction = new Transaction
        {
            Amount = amount,
            Description = description,
            TransactionDate = DateTime.UtcNow,
            AccountId = accountId,
            Status = Domain.Enums.TransactionStatus.Cleared,
            Source = Domain.Enums.TransactionSource.Manual,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Transactions.Add(transaction);
        await DbContext.SaveChangesAsync();
        return transaction;
    }

    protected async Task<Category> CreateTestCategoryAsync(string name = "Test Category")
    {
        var category = new Category
        {
            Name = name,
            Color = "#FF0000",
            IsSystemCategory = false,
            IsActive = true,
            UserId = TestUserId,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Categories.Add(category);
        await DbContext.SaveChangesAsync();
        return category;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        await _scope.DisposeAsync();
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
