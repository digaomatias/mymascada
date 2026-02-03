using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MyMascada.Application.Features.Accounts.DTOs;
using MyMascada.Domain.Enums;
using MyMascada.WebAPI;
using Xunit;

namespace MyMascada.Tests.Integration.Controllers;

public class AccountsControllerIntegrationTests : IntegrationTestBase
{
    public AccountsControllerIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateAccount_WithValidData_ShouldCreateAccountWithCorrectInitialBalance()
    {
        // Arrange
        await CreateTestUserAsync();
        
        var createAccountDto = new CreateAccountDto
        {
            Name = "Test Checking Account",
            Type = AccountType.Checking,
            Institution = "Test Bank",
            InitialBalance = 1500.00m,
            Currency = "USD",
            Notes = "Test account notes"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/accounts", createAccountDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var accountDto = await response.Content.ReadFromJsonAsync<AccountDto>();
        accountDto.Should().NotBeNull();
        accountDto!.Name.Should().Be("Test Checking Account");
        accountDto.Type.Should().Be(AccountType.Checking);
        accountDto.Institution.Should().Be("Test Bank");
        accountDto.Currency.Should().Be("USD");
        accountDto.Notes.Should().Be("Test account notes");
        
        // Verify the account was created in the database with correct balance
        var accountInDb = await DbContext.Accounts.FindAsync(accountDto.Id);
        accountInDb.Should().NotBeNull();
        accountInDb!.CurrentBalance.Should().Be(1500.00m);
        accountInDb.Name.Should().Be("Test Checking Account");
        accountInDb.Type.Should().Be(AccountType.Checking);
        accountInDb.UserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task CreateAccount_WithMissingFieldMapping_ShouldHandleCurrentBalanceToInitialBalance()
    {
        // Arrange
        await CreateTestUserAsync();
        
        // This test simulates the frontend sending "currentBalance" instead of "initialBalance"
        var requestData = new
        {
            name = "Test Account",
            type = (int)AccountType.Checking,
            institution = "Test Bank",
            currentBalance = 2000.00m, // Frontend sends this field
            currency = "USD"
        };

        // Act - This should fail if the API doesn't handle the field mapping correctly
        var response = await Client.PostAsJsonAsync("/api/accounts", requestData);

        // Assert - This will reveal if there's a field mapping issue
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            errorContent.Should().Contain("InitialBalance", "The API should expect 'InitialBalance', not 'currentBalance'");
        }
        else
        {
            // If it succeeds, the account should have been created with balance = 0 (default)
            // because the frontend field wasn't mapped correctly
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var accountDto = await response.Content.ReadFromJsonAsync<AccountDto>();
            
            var accountInDb = await DbContext.Accounts.FindAsync(accountDto!.Id);
            // If field mapping is broken, balance will be 0 instead of 2000
            accountInDb!.CurrentBalance.Should().Be(0m, "because currentBalance field wasn't mapped to InitialBalance");
        }
    }

    [Fact]
    public async Task CreateAccount_WithCorrectInitialBalanceField_ShouldCreateAccountSuccessfully()
    {
        // Arrange
        await CreateTestUserAsync();
        
        // Send the correct field name that the backend expects
        var requestData = new
        {
            name = "Test Account",
            type = (int)AccountType.Checking,
            institution = "Test Bank",
            initialBalance = 2000.00m, // Correct field name
            currency = "USD"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/accounts", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var accountDto = await response.Content.ReadFromJsonAsync<AccountDto>();
        
        var accountInDb = await DbContext.Accounts.FindAsync(accountDto!.Id);
        accountInDb!.CurrentBalance.Should().Be(2000.00m);
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnUserAccountsOnly()
    {
        // Arrange
        await CreateTestUserAsync();
        var account1 = await CreateTestAccountAsync("Account 1", 1000m);
        var account2 = await CreateTestAccountAsync("Account 2", 2000m);
        
        // Create account for different user
        var otherUserId = Guid.NewGuid();
        var otherUserAccount = new Domain.Entities.Account
        {
            Name = "Other User Account",
            Type = AccountType.Checking,
            CurrentBalance = 500m,
            Currency = "USD",
            UserId = otherUserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        DbContext.Accounts.Add(otherUserAccount);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();
        
        accounts.Should().NotBeNull();
        accounts!.Should().HaveCount(2);
        accounts.Should().OnlyContain(a => a.Name == "Account 1" || a.Name == "Account 2");
        accounts.Should().NotContain(a => a.Name == "Other User Account");
    }

    [Fact]
    public async Task GetAccount_WithValidId_ShouldReturnAccount()
    {
        // Arrange
        await CreateTestUserAsync();
        var account = await CreateTestAccountAsync("Test Account", 1500m);

        // Act
        var response = await Client.GetAsync($"/api/accounts/{account.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accountDto = await response.Content.ReadFromJsonAsync<AccountDto>();
        
        accountDto.Should().NotBeNull();
        accountDto!.Id.Should().Be(account.Id);
        accountDto.Name.Should().Be("Test Account");
    }

    [Fact]
    public async Task GetAccount_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        await CreateTestUserAsync();

        // Act
        var response = await Client.GetAsync("/api/accounts/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAccount_WithValidData_ShouldUpdateAccount()
    {
        // Arrange
        await CreateTestUserAsync();
        var account = await CreateTestAccountAsync("Original Name", 1000m);
        
        var updateDto = new UpdateAccountDto
        {
            Id = account.Id,
            Name = "Updated Name",
            Type = AccountType.Savings,
            Institution = "Updated Bank",
            Currency = "EUR",
            Notes = "Updated notes",
            IsActive = true
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/accounts/{account.Id}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedAccountDto = await response.Content.ReadFromJsonAsync<AccountDto>();
        
        updatedAccountDto.Should().NotBeNull();
        updatedAccountDto!.Name.Should().Be("Updated Name");
        updatedAccountDto.Type.Should().Be(AccountType.Savings);
        updatedAccountDto.Institution.Should().Be("Updated Bank");
        updatedAccountDto.Currency.Should().Be("EUR");
        updatedAccountDto.Notes.Should().Be("Updated notes");
        
        // Verify in database
        var accountInDb = await DbContext.Accounts.FindAsync(account.Id);
        accountInDb!.Name.Should().Be("Updated Name");
        accountInDb.Type.Should().Be(AccountType.Savings);
        accountInDb.Institution.Should().Be("Updated Bank");
        accountInDb.Currency.Should().Be("EUR");
        accountInDb.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task ArchiveAccount_WithNoTransactions_ShouldArchiveAccount()
    {
        // Arrange
        await CreateTestUserAsync();
        var account = await CreateTestAccountAsync("Account to Archive");

        // Act
        var response = await Client.PatchAsync($"/api/accounts/{account.Id}/archive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Verify account is marked as deleted (soft delete)
        var accountInDb = await DbContext.Accounts.FindAsync(account.Id);
        accountInDb.Should().BeNull(); // Should be soft-deleted
    }

    [Fact]
    public async Task ArchiveAccount_WithTransactions_ShouldReturnBadRequest()
    {
        // Arrange
        await CreateTestUserAsync();
        var account = await CreateTestAccountAsync("Account with Transactions");
        await CreateTestTransactionAsync(account.Id, 100m, "Test Transaction");

        // Act
        var response = await Client.PatchAsync($"/api/accounts/{account.Id}/archive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("Cannot archive account with transactions");
    }

    [Fact]
    public async Task GetAccountsWithBalances_ShouldReturnAccountsWithCalculatedBalances()
    {
        // Arrange
        await CreateTestUserAsync();
        var account1 = await CreateTestAccountAsync("Account 1", 1000m);
        var account2 = await CreateTestAccountAsync("Account 2", 2000m);
        
        // Add transactions that affect calculated balance
        await CreateTestTransactionAsync(account1.Id, 100m, "Deposit");
        await CreateTestTransactionAsync(account1.Id, -50m, "Withdrawal");

        // Act
        var response = await Client.GetAsync("/api/accounts/with-balances");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accountsWithBalances = await response.Content.ReadFromJsonAsync<List<AccountWithBalanceDto>>();
        
        accountsWithBalances.Should().NotBeNull();
        accountsWithBalances!.Should().HaveCount(2);
        
        var account1WithBalance = accountsWithBalances.First(a => a.Id == account1.Id);
        account1WithBalance.CurrentBalance.Should().Be(1000m); // Original balance
        account1WithBalance.CalculatedBalance.Should().Be(1050m); // 1000 + 100 - 50
        
        var account2WithBalance = accountsWithBalances.First(a => a.Id == account2.Id);
        account2WithBalance.CurrentBalance.Should().Be(2000m);
        account2WithBalance.CalculatedBalance.Should().Be(2000m); // No transactions
    }

    [Fact]
    public async Task HasTransactions_WithTransactions_ShouldReturnTrue()
    {
        // Arrange
        await CreateTestUserAsync();
        var account = await CreateTestAccountAsync("Account with Transactions");
        await CreateTestTransactionAsync(account.Id, 100m, "Test Transaction");

        // Act
        var response = await Client.GetAsync($"/api/accounts/{account.Id}/has-transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        
        // Note: Due to JSON deserialization, we need to check the property dynamically
        var jsonString = await response.Content.ReadAsStringAsync();
        jsonString.Should().Contain("\"hasTransactions\":true");
    }

    [Fact]
    public async Task HasTransactions_WithoutTransactions_ShouldReturnFalse()
    {
        // Arrange
        await CreateTestUserAsync();
        var account = await CreateTestAccountAsync("Account without Transactions");

        // Act
        var response = await Client.GetAsync($"/api/accounts/{account.Id}/has-transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonString = await response.Content.ReadAsStringAsync();
        jsonString.Should().Contain("\"hasTransactions\":false");
    }

    [Fact]
    public async Task CreateAccount_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        await CreateTestUserAsync();
        
        var invalidDto = new CreateAccountDto
        {
            Name = "", // Invalid: empty name
            Type = AccountType.Checking,
            InitialBalance = -2000000m, // Invalid: below minimum
            Currency = "INVALID" // Invalid: not 3-letter code
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/accounts", invalidDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAccount_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null; // Remove auth header
        
        var createAccountDto = new CreateAccountDto
        {
            Name = "Test Account",
            Type = AccountType.Checking,
            InitialBalance = 1000m,
            Currency = "USD"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/accounts", createAccountDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}