using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Services;

public class LlmDescriptionCleaningServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmDescriptionCleaningService> _logger;

    public LlmDescriptionCleaningServiceTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<LlmDescriptionCleaningService>>();

        // Setup configuration with test API key
        _configuration["LLM:OpenAI:ApiKey"].Returns("sk-test-not-a-real-key-for-unit-tests-only");
        _configuration["LLM:OpenAI:Model"].Returns("gpt-4o-mini");
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ShouldThrowException()
    {
        // Arrange
        var badConfig = Substitute.For<IConfiguration>();
        badConfig["LLM:OpenAI:ApiKey"].Returns((string?)null);

        // Act & Assert
        Action act = () => new LlmDescriptionCleaningService(badConfig, _logger);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("OpenAI API key is not configured");
    }

    [Fact]
    public void Constructor_WithValidApiKey_ShouldNotThrow()
    {
        // Act & Assert
        Action act = () => new LlmDescriptionCleaningService(_configuration, _logger);
        act.Should().NotThrow();
    }

    [Fact(Skip = "Requires real OpenAI API key - integration test, not unit test")]
    public async Task IsServiceAvailableAsync_WithValidApiKey_ShouldReturnTrue()
    {
        // Arrange
        var service = new LlmDescriptionCleaningService(_configuration, _logger);

        // Act
        var result = await service.IsServiceAvailableAsync();

        // Assert
        result.Should().BeTrue("LLM service should be available with valid API key");
    }

    [Fact(Skip = "Requires real OpenAI API key - integration test, not unit test")]
    public async Task CleanDescriptionsAsync_WithSampleData_ShouldReturnValidResponse()
    {
        // Arrange
        var service = new LlmDescriptionCleaningService(_configuration, _logger);

        var descriptions = new List<DescriptionCleaningInput>
        {
            new DescriptionCleaningInput
            {
                TransactionId = 1,
                OriginalDescription = "POS 4829 COUNTDOWN AUCKLAND NZ 23/01"
            },
            new DescriptionCleaningInput
            {
                TransactionId = 2,
                OriginalDescription = "AMZN MKTP US*RT4K92JF0 AMZN.COM/BILLWA"
            }
        };

        // Act
        var result = await service.CleanDescriptionsAsync(descriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Results.Should().HaveCount(2);
        result.ProcessingTimeMs.Should().BeGreaterThan(0);
    }
}

public class NoOpDescriptionCleaningServiceTests
{
    private readonly ILogger<NoOpDescriptionCleaningService> _logger;

    public NoOpDescriptionCleaningServiceTests()
    {
        _logger = Substitute.For<ILogger<NoOpDescriptionCleaningService>>();
    }

    [Fact]
    public async Task CleanDescriptionsAsync_ShouldReturnFailureResponse()
    {
        // Arrange
        var service = new NoOpDescriptionCleaningService(_logger);

        var descriptions = new List<DescriptionCleaningInput>
        {
            new DescriptionCleaningInput
            {
                TransactionId = 1,
                OriginalDescription = "Test"
            }
        };

        // Act
        var result = await service.CleanDescriptionsAsync(descriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("not configured");
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ShouldReturnFalse()
    {
        // Arrange
        var service = new NoOpDescriptionCleaningService(_logger);

        // Act
        var result = await service.IsServiceAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }
}
