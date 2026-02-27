using System.Security.Claims;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Accounts.DTOs;
using MyMascada.Application.Features.Accounts.Mappings;
using MyMascada.Application.Features.Accounts.Queries;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.WebAPI.Controllers;

namespace MyMascada.Tests.Unit.Controllers;

public class AccountsControllerTests
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccountAccessService _accountAccess;
    private readonly IAccountShareRepository _accountShareRepository;
    private readonly AccountsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public AccountsControllerTests()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _mediator = Substitute.For<IMediator>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetUserId().Returns(_userId);
        _accountAccess = Substitute.For<IAccountAccessService>();
        _accountAccess.IsOwnerAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);
        _accountShareRepository = Substitute.For<IAccountShareRepository>();

        // Set up real AutoMapper with the account mapping profile
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AccountMappingProfile>();
        });
        _mapper = configuration.CreateMapper();

        _controller = new AccountsController(_accountRepository, _transactionRepository, _mapper, _mediator, _currentUserService, _accountAccess, _accountShareRepository);

        SetupUserClaims();
    }

    private void SetupUserClaims()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnUserAccounts()
    {
        // Arrange
        var expectedAccounts = new List<Account>
        {
            new() { Id = 1, Name = "Checking Account", Type = AccountType.Checking, UserId = _userId },
            new() { Id = 2, Name = "Savings Account", Type = AccountType.Savings, UserId = _userId }
        };

        _accountRepository.GetByUserIdAsync(_userId)
            .Returns(expectedAccounts);

        // Act
        var result = await _controller.GetAccounts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var accounts = okResult.Value.Should().BeAssignableTo<IEnumerable<AccountDto>>().Subject;
        accounts.Should().HaveCount(2);

        await _accountRepository.Received(1).GetByUserIdAsync(_userId);
    }

    [Fact]
    public async Task GetAccount_WithValidId_ShouldReturnAccount()
    {
        // Arrange
        var accountId = 1;
        var expectedAccount = new Account
        {
            Id = accountId,
            Name = "Test Account",
            UserId = _userId
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(expectedAccount);

        // Act
        var result = await _controller.GetAccount(accountId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var account = okResult.Value.Should().BeOfType<AccountDto>().Subject;
        account.Id.Should().Be(accountId);

        await _accountRepository.Received(1).GetByIdAsync(accountId, _userId);
    }

    [Fact]
    public async Task GetAccount_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var accountId = 999;
        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns((Account?)null);

        // Act
        var result = await _controller.GetAccount(accountId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateAccount_WithValidRequest_ShouldReturnCreatedAccount()
    {
        // Arrange
        var request = new CreateAccountDto
        {
            Name = "New Account",
            Type = AccountType.Checking,
            Institution = "Test Bank",
            LastFourDigits = "1234",
            InitialBalance = 1000.00m,
            Currency = "USD",
            Notes = "Test notes"
        };

        var expectedAccount = new Account
        {
            Id = 1,
            Name = request.Name,
            Type = request.Type,
            Institution = request.Institution,
            LastFourDigits = request.LastFourDigits,
            CurrentBalance = request.InitialBalance,
            Currency = request.Currency,
            IsActive = true,
            Notes = request.Notes,
            UserId = _userId
        };

        _accountRepository.AddAsync(Arg.Any<Account>())
            .Returns(expectedAccount);

        // Act
        var result = await _controller.CreateAccount(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AccountsController.GetAccount));
        createdResult.RouteValues!["id"].Should().Be(1);

        var account = createdResult.Value.Should().BeOfType<AccountDto>().Subject;
        account.Name.Should().Be(request.Name);
        account.Type.Should().Be(request.Type);
        account.IsActive.Should().BeTrue();

        await _accountRepository.Received(1).AddAsync(Arg.Is<Account>(a =>
            a.Name == request.Name &&
            a.Type == request.Type &&
            a.Institution == request.Institution &&
            a.LastFourDigits == request.LastFourDigits &&
            a.CurrentBalance == request.InitialBalance &&
            a.Currency == request.Currency &&
            a.Notes == request.Notes &&
            a.UserId == _userId &&
            a.IsActive == true));
    }

    [Fact]
    public async Task CreateAccount_WithDefaultCurrency_ShouldUseUSD()
    {
        // Arrange
        var request = new CreateAccountDto
        {
            Name = "Test Account",
            Type = AccountType.Checking
            // Currency will default to "USD"
        };

        var expectedAccount = new Account { Id = 1, Currency = "USD" };
        _accountRepository.AddAsync(Arg.Any<Account>())
            .Returns(expectedAccount);

        // Act
        await _controller.CreateAccount(request);

        // Assert
        await _accountRepository.Received(1).AddAsync(Arg.Is<Account>(a =>
            a.Currency == "USD"));
    }

    [Fact]
    public async Task UpdateAccount_WithValidRequest_ShouldReturnUpdatedAccount()
    {
        // Arrange
        var accountId = 1;
        var request = new UpdateAccountDto
        {
            Id = accountId,
            Name = "Updated Account",
            Type = AccountType.Checking,
            Institution = "Updated Bank",
            LastFourDigits = "5678",
            Notes = "Updated notes",
            Currency = "EUR",
            IsActive = true
        };

        var existingAccount = new Account
        {
            Id = accountId,
            Name = "Original Account",
            Currency = "USD",
            UserId = _userId
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(existingAccount);
        
        _transactionRepository.GetByAccountIdAsync(accountId, _userId)
            .Returns(new List<Transaction>()); // No transactions

        _accountRepository.UpdateAsync(Arg.Any<Account>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateAccount(accountId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var account = okResult.Value.Should().BeOfType<AccountDto>().Subject;
        
        account.Name.Should().Be(request.Name);
        account.Institution.Should().Be(request.Institution);
        account.LastFourDigits.Should().Be(request.LastFourDigits);
        account.Notes.Should().Be(request.Notes);

        await _accountRepository.Received(1).UpdateAsync(existingAccount);
    }

    [Fact]
    public async Task UpdateAccount_WithTransactions_ShouldAllowUpdate()
    {
        // Arrange
        var accountId = 1;
        var request = new UpdateAccountDto
        {
            Id = accountId,
            Name = "Updated Account",
            Type = AccountType.Checking,
            Currency = "USD",
            IsActive = true
        };

        var existingAccount = new Account
        {
            Id = accountId,
            Name = "Original Account",
            Currency = "USD",
            UserId = _userId
        };

        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = accountId }
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(existingAccount);
        
        _transactionRepository.GetByAccountIdAsync(accountId, _userId)
            .Returns(transactions);

        // Act
        var result = await _controller.UpdateAccount(accountId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var updatedAccount = okResult.Value.Should().BeOfType<AccountDto>().Subject;
        
        updatedAccount.Name.Should().Be("Updated Account");
        // Currency should be updated to USD (from EUR request) since we now support currency updates
        updatedAccount.Currency.Should().Be("USD");

        await _accountRepository.Received(1).UpdateAsync(Arg.Is<Account>(a => 
            a.Name == "Updated Account"));
    }

    [Fact]
    public async Task UpdateAccount_WithCurrencyChange_ShouldUpdateCurrency()
    {
        // Arrange
        var accountId = 1;
        var request = new UpdateAccountDto
        {
            Id = accountId,
            Name = "Test Account",
            Type = AccountType.Checking,
            Currency = "EUR",
            IsActive = true
        };

        var existingAccount = new Account
        {
            Id = accountId,
            Name = "Test Account",
            Currency = "USD",
            UserId = _userId
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(existingAccount);

        _accountRepository.UpdateAsync(Arg.Any<Account>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateAccount(accountId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var account = okResult.Value.Should().BeOfType<AccountDto>().Subject;
        
        account.Currency.Should().Be("EUR");

        await _accountRepository.Received(1).UpdateAsync(Arg.Is<Account>(a => 
            a.Currency == "EUR"));
    }

    [Fact]
    public async Task UpdateAccount_WithNonExistentAccount_ShouldReturnNotFound()
    {
        // Arrange
        var accountId = 999;
        var request = new UpdateAccountDto 
        { 
            Id = accountId,
            Name = "Test",
            Type = AccountType.Checking,
            Currency = "USD",
            IsActive = true
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns((Account?)null);

        // Act
        var result = await _controller.UpdateAccount(accountId, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ArchiveAccount_WithNoTransactions_ShouldReturnNoContent()
    {
        // Arrange
        var accountId = 1;
        var account = new Account
        {
            Id = accountId,
            Name = "Test Account",
            IsActive = true,
            UserId = _userId
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(account);
        
        _transactionRepository.GetByAccountIdAsync(accountId, _userId)
            .Returns(new List<Transaction>());

        _accountRepository.DeleteAsync(Arg.Any<Account>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ArchiveAccount(accountId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        await _accountRepository.Received(1).DeleteAsync(account);
    }

    [Fact]
    public async Task ArchiveAccount_WithTransactions_ShouldReturnBadRequest()
    {
        // Arrange
        var accountId = 1;
        var account = new Account
        {
            Id = accountId,
            UserId = _userId
        };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(account);

        _transactionRepository.HasTransactionsAsync(accountId, _userId)
            .Returns(true);

        // Act
        var result = await _controller.ArchiveAccount(accountId);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        var details = errorResponse.GetType().GetProperty("details")?.GetValue(errorResponse);
        
        message.Should().Be("Cannot archive account with transactions");
        details.Should().Be("Transaction history must be preserved for data integrity");

        await _accountRepository.DidNotReceive().UpdateAsync(Arg.Any<Account>());
    }

    [Fact]
    public async Task ArchiveAccount_WithNonExistentAccount_ShouldReturnNotFound()
    {
        // Arrange
        var accountId = 999;
        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns((Account?)null);

        // Act
        var result = await _controller.ArchiveAccount(accountId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task HasTransactions_WithTransactions_ShouldReturnTrue()
    {
        // Arrange
        var accountId = 1;
        var account = new Account { Id = accountId, UserId = _userId };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(account);

        _transactionRepository.HasTransactionsAsync(accountId, _userId)
            .Returns(true);

        // Act
        var result = await _controller.HasTransactions(accountId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var hasTransactions = response.GetType().GetProperty("hasTransactions")?.GetValue(response);
        hasTransactions.Should().Be(true);
    }

    [Fact]
    public async Task HasTransactions_WithoutTransactions_ShouldReturnFalse()
    {
        // Arrange
        var accountId = 1;
        var account = new Account { Id = accountId, UserId = _userId };

        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns(account);
        
        _transactionRepository.GetByAccountIdAsync(accountId, _userId)
            .Returns(new List<Transaction>());

        // Act
        var result = await _controller.HasTransactions(accountId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var hasTransactions = response.GetType().GetProperty("hasTransactions")?.GetValue(response);
        hasTransactions.Should().Be(false);
    }

    [Fact]
    public async Task HasTransactions_WithNonExistentAccount_ShouldReturnNotFound()
    {
        // Arrange
        var accountId = 999;
        _accountRepository.GetByIdAsync(accountId, _userId)
            .Returns((Account?)null);

        // Act
        var result = await _controller.HasTransactions(accountId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAccountsWithBalances_ShouldReturnAccountsWithCalculatedBalances()
    {
        // Arrange
        var accounts = new List<Account>
        {
            new() { Id = 1, Name = "Checking", Type = AccountType.Checking, CurrentBalance = 1000m, UserId = _userId },
            new() { Id = 2, Name = "Savings", Type = AccountType.Savings, CurrentBalance = 5000m, UserId = _userId }
        };

        var balances = new Dictionary<int, decimal>
        {
            { 1, 950m }, // Calculated balance differs from current
            { 2, 5000m }
        };

        _accountRepository.GetByUserIdAsync(_userId)
            .Returns(accounts);
        
        _transactionRepository.GetAccountBalancesAsync(_userId)
            .Returns(balances);

        // Act
        var result = await _controller.GetAccountsWithBalances();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var accountsWithBalances = okResult.Value.Should().BeAssignableTo<IEnumerable<AccountWithBalanceDto>>().Subject;
        var accountsList = accountsWithBalances.ToList();
        
        accountsList.Should().HaveCount(2);
        
        var checkingAccount = accountsList.First(a => a.Id == 1);
        checkingAccount.Name.Should().Be("Checking");
        checkingAccount.CurrentBalance.Should().Be(1000m);
        checkingAccount.CalculatedBalance.Should().Be(950m);
        
        var savingsAccount = accountsList.First(a => a.Id == 2);
        savingsAccount.Name.Should().Be("Savings");
        savingsAccount.CurrentBalance.Should().Be(5000m);
        savingsAccount.CalculatedBalance.Should().Be(5000m);

        await _accountRepository.Received(1).GetByUserIdAsync(_userId);
        await _transactionRepository.Received(1).GetAccountBalancesAsync(_userId);
    }

    [Fact]
    public async Task GetAccountsWithBalances_WithMissingBalance_ShouldDefaultToZero()
    {
        // Arrange
        var accounts = new List<Account>
        {
            new() { Id = 1, Name = "Test Account", CurrentBalance = 1000m, UserId = _userId }
        };

        var balances = new Dictionary<int, decimal>(); // No balance for account 1

        _accountRepository.GetByUserIdAsync(_userId)
            .Returns(accounts);
        
        _transactionRepository.GetAccountBalancesAsync(_userId)
            .Returns(balances);

        // Act
        var result = await _controller.GetAccountsWithBalances();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var accountsWithBalances = okResult.Value.Should().BeAssignableTo<IEnumerable<AccountWithBalanceDto>>().Subject;
        var account = accountsWithBalances.First();
        
        account.CalculatedBalance.Should().Be(0m);
    }

    [Fact]
    public async Task GetUserId_WithMissingClaim_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Mock ICurrentUserService to throw when user is not authenticated
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(_ => throw new UnauthorizedAccessException("Invalid user ID in token"));

        var controller = new AccountsController(_accountRepository, _transactionRepository, _mapper, _mediator, currentUserService, _accountAccess, _accountShareRepository);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await controller.GetAccounts();
        });

        exception.Message.Should().Be("Invalid user ID in token");
    }

    [Fact]
    public async Task GetUserId_WithInvalidClaim_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Mock ICurrentUserService to throw when claims are invalid
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(_ => throw new UnauthorizedAccessException("Invalid user ID in token"));

        var controller = new AccountsController(_accountRepository, _transactionRepository, _mapper, _mediator, currentUserService, _accountAccess, _accountShareRepository);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await controller.GetAccounts();
        });

        exception.Message.Should().Be("Invalid user ID in token");
    }

    [Fact]
    public async Task GetAccountDetails_WithValidAccount_ShouldReturnAccountDetails()
    {
        // Arrange
        var accountId = 1;
        var accountDetailsDto = new AccountDetailsDto
        {
            Id = accountId,
            Name = "Test Account",
            CurrentBalance = 1000m,
            CalculatedBalance = 950m,
            MonthlySpending = new MonthlySpendingDto
            {
                CurrentMonthSpending = 300m,
                PreviousMonthSpending = 250m,
                ChangeAmount = 50m,
                ChangePercentage = 20m,
                TrendDirection = "up",
                MonthName = "January",
                Year = 2025
            }
        };

        _mediator.Send(Arg.Any<GetAccountDetailsQuery>())
            .Returns(accountDetailsDto);

        // Act
        var result = await _controller.GetAccountDetails(accountId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var details = okResult.Value.Should().BeOfType<AccountDetailsDto>().Subject;
        
        details.Id.Should().Be(accountId);
        details.MonthlySpending.CurrentMonthSpending.Should().Be(300m);
        details.MonthlySpending.PreviousMonthSpending.Should().Be(250m);
        details.MonthlySpending.ChangePercentage.Should().Be(20m);
        details.MonthlySpending.TrendDirection.Should().Be("up");

        await _mediator.Received(1).Send(Arg.Is<GetAccountDetailsQuery>(q => 
            q.AccountId == accountId && q.UserId == _userId));
    }

    [Fact]
    public async Task GetAccountDetails_WithNonExistentAccount_ShouldReturnNotFound()
    {
        // Arrange
        var accountId = 999;
        _mediator.Send(Arg.Any<GetAccountDetailsQuery>())
            .Returns((AccountDetailsDto?)null);

        // Act
        var result = await _controller.GetAccountDetails(accountId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}