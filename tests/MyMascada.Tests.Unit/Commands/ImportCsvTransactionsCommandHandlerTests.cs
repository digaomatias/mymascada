using System.Text;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.CsvImport.Commands;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.Transactions.Services;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Commands;

public class ImportCsvTransactionsCommandHandlerTests
{
    private readonly ICsvImportService _csvImportService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly TransactionReviewService _reviewService;
    private readonly ICategorizationPipeline _categorizationPipeline;
    private readonly ImportCsvTransactionsCommandHandler _handler;

    public ImportCsvTransactionsCommandHandlerTests()
    {
        _csvImportService = Substitute.For<ICsvImportService>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _reviewService = Substitute.For<TransactionReviewService>(_transactionRepository, _accountRepository);
        _categorizationPipeline = Substitute.For<ICategorizationPipeline>();
        
        _handler = new ImportCsvTransactionsCommandHandler(
            _csvImportService,
            _transactionRepository,
            _accountRepository,
            _reviewService,
            _categorizationPipeline);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldImportTransactions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountId = 1;
        var csvData = Encoding.UTF8.GetBytes("Date,Description,Amount\n2025-06-18,Test,-10.00");
        
        var command = new ImportCsvTransactionsCommand
        {
            UserId = userId,
            AccountId = accountId,
            CsvData = csvData,
            FileName = "test.csv",
            Format = CsvFormat.Generic,
            HasHeader = true
        };

        var account = new Account { Id = accountId, UserId = userId };
        var parseResult = new CsvParseResult
        {
            IsSuccess = true,
            TotalRows = 2,
            ValidRows = 1,
            Transactions = new List<CsvTransactionRow>
            {
                new CsvTransactionRow
                {
                    Date = new DateTime(2025, 6, 18),
                    Description = "Test",
                    Amount = -10.00m,
                    ExternalId = "TEST123",
                    Status = TransactionStatus.Cleared
                }
            }
        };

        _accountRepository.GetByIdAsync(accountId, userId)
            .Returns(account);
        
        _csvImportService.ValidateFileAsync(Arg.Any<Stream>())
            .Returns(true);
        
        _csvImportService.GetDefaultMapping(CsvFormat.Generic)
            .Returns(new CsvFieldMapping());
        
        _csvImportService.ParseCsvAsync(Arg.Any<Stream>(), Arg.Any<CsvFieldMapping>(), true)
            .Returns(parseResult);
        
        _transactionRepository.ExistsByExternalIdAsync("TEST123", accountId)
            .Returns(false);
        
        _transactionRepository.AddAsync(Arg.Any<Transaction>())
            .Returns(callInfo => 
            {
                var transaction = callInfo.Arg<Transaction>();
                transaction.Id = 1;
                return transaction;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProcessedRows.Should().Be(1);
        result.TotalRows.Should().Be(2);
        result.ImportedTransactions.Should().HaveCount(1);
        result.ImportedTransactions.First().IsNew.Should().BeTrue();

        await _transactionRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => 
            t.Description == "Test" && 
            t.Amount == -10.00m && 
            t.ExternalId == "TEST123"));
    }

    [Fact]
    public async Task Handle_WithNonExistentAccount_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountId = 999;
        var csvData = Encoding.UTF8.GetBytes("Date,Description,Amount\n2025-06-18,Test,-10.00");
        
        var command = new ImportCsvTransactionsCommand
        {
            UserId = userId,
            AccountId = accountId,
            CsvData = csvData
        };

        _accountRepository.GetByIdAsync(accountId, userId)
            .Returns((Account?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain($"Account with ID {accountId} not found or does not belong to user");
        
        await _csvImportService.DidNotReceive().ValidateFileAsync(Arg.Any<Stream>());
    }

    [Fact]
    public async Task Handle_WithInvalidCsvFile_ShouldReturnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountId = 1;
        var csvData = Encoding.UTF8.GetBytes("invalid csv content");
        
        var command = new ImportCsvTransactionsCommand
        {
            UserId = userId,
            AccountId = accountId,
            CsvData = csvData
        };

        var account = new Account { Id = accountId, UserId = userId };
        
        _accountRepository.GetByIdAsync(accountId, userId)
            .Returns(account);
        
        _csvImportService.ValidateFileAsync(Arg.Any<Stream>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Invalid CSV file format");
        
        await _csvImportService.DidNotReceive().ParseCsvAsync(Arg.Any<Stream>(), Arg.Any<CsvFieldMapping>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_WithDuplicateTransaction_ShouldSkip()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountId = 1;
        var csvData = Encoding.UTF8.GetBytes("Date,Description,Amount\n2025-06-18,Test,-10.00");
        
        var command = new ImportCsvTransactionsCommand
        {
            UserId = userId,
            AccountId = accountId,
            CsvData = csvData,
            SkipDuplicates = true
        };

        var account = new Account { Id = accountId, UserId = userId };
        var parseResult = new CsvParseResult
        {
            IsSuccess = true,
            TotalRows = 2,
            ValidRows = 1,
            Transactions = new List<CsvTransactionRow>
            {
                new CsvTransactionRow
                {
                    Date = new DateTime(2025, 6, 18),
                    Description = "Test",
                    Amount = -10.00m,
                    ExternalId = "TEST123",
                    Status = TransactionStatus.Cleared
                }
            }
        };

        _accountRepository.GetByIdAsync(accountId, userId)
            .Returns(account);
        
        _csvImportService.ValidateFileAsync(Arg.Any<Stream>())
            .Returns(true);
        
        _csvImportService.ParseCsvAsync(Arg.Any<Stream>(), Arg.Any<CsvFieldMapping>(), Arg.Any<bool>())
            .Returns(parseResult);
        
        _transactionRepository.ExistsByExternalIdAsync("TEST123", accountId)
            .Returns(true); // Duplicate exists

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.SkippedRows.Should().Be(1);
        result.ProcessedRows.Should().Be(0);
        result.ImportedTransactions.Should().HaveCount(1);
        result.ImportedTransactions.First().IsSkipped.Should().BeTrue();
        result.ImportedTransactions.First().SkipReason.Should().Be("Duplicate transaction (same external ID)");

        await _transactionRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>());
    }
}