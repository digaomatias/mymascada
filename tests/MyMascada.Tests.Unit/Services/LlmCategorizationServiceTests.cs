using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Services;

public class LlmCategorizationServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmCategorizationService> _logger;

    public LlmCategorizationServiceTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<LlmCategorizationService>>();
        
        // Setup configuration with test API key
        _configuration["LLM:OpenAI:ApiKey"].Returns("sk-test-not-a-real-key-for-unit-tests-only");
        _configuration["LLM:OpenAI:Model"].Returns("gpt-4o-mini");
    }

    [Fact]
    public async Task IsServiceAvailableAsync_WithValidApiKey_ShouldReturnTrue()
    {
        // Arrange
        var service = new LlmCategorizationService(_configuration, _logger);

        // Act
        var result = await service.IsServiceAvailableAsync();

        // Assert
        result.Should().BeTrue("LLM service should be available with valid API key");
    }

    [Fact]
    public async Task CategorizeTransactionsAsync_WithSampleData_ShouldReturnValidResponse()
    {
        // Arrange
        var service = new LlmCategorizationService(_configuration, _logger);
        
        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Id = 1,
                Description = "AMAZON.COM AMZN.COM/BILL WA",
                Amount = -85.50m,
                TransactionDate = DateTime.Now.AddDays(-1),
                AccountId = 1
            },
            new Transaction
            {
                Id = 2,
                Description = "STARBUCKS STORE #12345",
                Amount = -4.50m,
                TransactionDate = DateTime.Now.AddDays(-2),
                AccountId = 1
            }
        };

        var categories = new List<Category>
        {
            new Category
            {
                Id = 1,
                Name = "Online Shopping",
                Type = CategoryType.Expense,
                Color = "#3B82F6"
            },
            new Category
            {
                Id = 2,
                Name = "Coffee & Cafes",
                Type = CategoryType.Expense,
                Color = "#8B5CF6"
            },
            new Category
            {
                Id = 3,
                Name = "Food & Dining",
                Type = CategoryType.Expense,
                Color = "#EF4444",
                ParentCategoryId = null
            }
        };

        var rules = new List<CategorizationRule>
        {
            new CategorizationRule
            {
                Id = 1,
                Name = "Amazon Rule",
                Pattern = "AMAZON",
                Type = RuleType.Contains,
                CategoryId = 1,
                ConfidenceScore = 0.9,
                MatchCount = 15,
                CorrectionCount = 1,
                UserId = Guid.NewGuid()
            }
        };

        // Act
        var result = await service.CategorizeTransactionsAsync(transactions, categories);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Categorization should succeed with valid data");
        result.Categorizations.Should().HaveCount(2, "Should return categorizations for both transactions");
        
        // Check Amazon transaction
        var amazonCategorization = result.Categorizations.FirstOrDefault(c => c.TransactionId == 1);
        amazonCategorization.Should().NotBeNull();
        amazonCategorization!.Suggestions.Should().NotBeEmpty("Should have suggestions for Amazon transaction");
        
        // Check Starbucks transaction
        var starbucksCategorization = result.Categorizations.FirstOrDefault(c => c.TransactionId == 2);
        starbucksCategorization.Should().NotBeNull();
        starbucksCategorization!.Suggestions.Should().NotBeEmpty("Should have suggestions for Starbucks transaction");

        // Verify summary
        result.Summary.Should().NotBeNull();
        result.Summary.TotalProcessed.Should().Be(2);
        result.Summary.ProcessingTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ShouldThrowException()
    {
        // Arrange
        var badConfig = Substitute.For<IConfiguration>();
        badConfig["LLM:OpenAI:ApiKey"].Returns((string?)null);

        // Act & Assert
        Action act = () => new LlmCategorizationService(badConfig, _logger);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("OpenAI API key is not configured");
    }
}