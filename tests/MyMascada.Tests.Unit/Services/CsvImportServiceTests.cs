using System.Text;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Infrastructure.Services.CsvImport;

namespace MyMascada.Tests.Unit.Services;

public class CsvImportServiceTests
{
    private readonly CsvImportService _sut;

    public CsvImportServiceTests()
    {
        _sut = new CsvImportService();
    }

    [Fact]
    public async Task ParseCsvAsync_WithValidGenericCsv_ShouldReturnCorrectTransactions()
    {
        // Arrange
        const string csvContent = """
            Date,Description,Amount,Reference
            2025-06-18,Grocery Store,-87.43,TXN001
            2025-06-17,Gas Station,-45.20,TXN002
            2025-06-16,Salary Deposit,2500.00,TXN003
            2025-06-15,Coffee Shop,-5.75,TXN004
            """;
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var mapping = _sut.GetDefaultMapping(CsvFormat.Generic);

        // Act
        var result = await _sut.ParseCsvAsync(stream, mapping, hasHeader: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.TotalRows.Should().Be(5); // Including header
        result.ValidRows.Should().Be(4);
        result.Transactions.Should().HaveCount(4);
        result.Errors.Should().BeEmpty();

        var transactions = result.Transactions.ToList();
        
        transactions[0].Description.Should().Be("Grocery Store");
        transactions[0].Amount.Should().Be(-87.43m);
        transactions[0].Date.Should().Be(new DateTime(2025, 6, 18));
        
        transactions[1].Description.Should().Be("Gas Station");
        transactions[1].Amount.Should().Be(-45.20m);
        
        transactions[2].Description.Should().Be("Salary Deposit");
        transactions[2].Amount.Should().Be(2500.00m);
        
        transactions[3].Description.Should().Be("Coffee Shop");
        transactions[3].Amount.Should().Be(-5.75m);
    }

    [Fact]
    public async Task ValidateFileAsync_WithValidCsv_ShouldReturnTrue()
    {
        // Arrange
        const string csvContent = """
            Date,Description,Amount
            2025-06-18,Test Transaction,-10.00
            """;
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _sut.ValidateFileAsync(stream);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateFileAsync_WithEmptyFile_ShouldReturnFalse()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));

        // Act
        var result = await _sut.ValidateFileAsync(stream);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateFileAsync_WithNonCsvContent_ShouldReturnFalse()
    {
        // Arrange
        const string nonCsvContent = "This is not a CSV file";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(nonCsvContent));

        // Act
        var result = await _sut.ValidateFileAsync(stream);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(CsvFormat.Generic)]
    [InlineData(CsvFormat.Chase)]
    [InlineData(CsvFormat.WellsFargo)]
    [InlineData(CsvFormat.BankOfAmerica)]
    [InlineData(CsvFormat.Mint)]
    [InlineData(CsvFormat.Quicken)]
    public void GetDefaultMapping_ForAllFormats_ShouldReturnValidMapping(CsvFormat format)
    {
        // Act
        var mapping = _sut.GetDefaultMapping(format);

        // Assert
        mapping.Should().NotBeNull();
        mapping.DateColumn.Should().BeGreaterOrEqualTo(0);
        mapping.DescriptionColumn.Should().BeGreaterOrEqualTo(0);
        mapping.AmountColumn.Should().BeGreaterOrEqualTo(0);
        mapping.DateFormat.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateExternalId_WithSameData_ShouldReturnSameId()
    {
        // Arrange
        var row1 = new CsvTransactionRow
        {
            Date = new DateTime(2025, 6, 18),
            Amount = -87.43m,
            Description = "Grocery Store"
        };
        
        var row2 = new CsvTransactionRow
        {
            Date = new DateTime(2025, 6, 18),
            Amount = -87.43m,
            Description = "Grocery Store"
        };

        // Act
        var id1 = _sut.GenerateExternalId(row1);
        var id2 = _sut.GenerateExternalId(row2);

        // Assert
        id1.Should().Be(id2);
        id1.Should().HaveLength(16); // First 16 characters of SHA256 hash
    }

    [Fact]
    public void GenerateExternalId_WithDifferentData_ShouldReturnDifferentIds()
    {
        // Arrange
        var row1 = new CsvTransactionRow
        {
            Date = new DateTime(2025, 6, 18),
            Amount = -87.43m,
            Description = "Grocery Store"
        };
        
        var row2 = new CsvTransactionRow
        {
            Date = new DateTime(2025, 6, 18),
            Amount = -45.20m, // Different amount
            Description = "Grocery Store"
        };

        // Act
        var id1 = _sut.GenerateExternalId(row1);
        var id2 = _sut.GenerateExternalId(row2);

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public async Task ParseCsvAsync_WithInvalidDateFormat_ShouldReportError()
    {
        // Arrange
        const string csvContent = """
            Date,Description,Amount
            invalid-date,Test Transaction,-10.00
            """;
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var mapping = _sut.GetDefaultMapping(CsvFormat.Generic);

        // Act
        var result = await _sut.ParseCsvAsync(stream, mapping, hasHeader: true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error => error.Contains("Invalid date format"));
    }

    [Fact]
    public async Task ParseCsvAsync_WithInvalidAmountFormat_ShouldReportError()
    {
        // Arrange
        const string csvContent = """
            Date,Description,Amount
            2025-06-18,Test Transaction,invalid-amount
            """;
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var mapping = _sut.GetDefaultMapping(CsvFormat.Generic);

        // Act
        var result = await _sut.ParseCsvAsync(stream, mapping, hasHeader: true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(error => error.Contains("Invalid amount format"));
    }
}