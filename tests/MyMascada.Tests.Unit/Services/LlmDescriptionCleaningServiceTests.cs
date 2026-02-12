using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Services;

public class LlmDescriptionCleaningServiceTests
{
    private readonly IUserAiKernelFactory _kernelFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LlmDescriptionCleaningService> _logger;
    private readonly Guid _testUserId = Guid.NewGuid();

    public LlmDescriptionCleaningServiceTests()
    {
        _kernelFactory = Substitute.For<IUserAiKernelFactory>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<LlmDescriptionCleaningService>>();

        _currentUserService.GetUserId().Returns(_testUserId);
    }

    [Fact]
    public async Task CleanDescriptionsAsync_WhenNoKernel_ShouldReturnFailure()
    {
        // Arrange
        _kernelFactory.CreateKernelForUserAsync(_testUserId).Returns((Kernel?)null);
        var service = new LlmDescriptionCleaningService(_kernelFactory, _currentUserService, _logger);

        var descriptions = new List<DescriptionCleaningInput>
        {
            new DescriptionCleaningInput
            {
                TransactionId = 1,
                OriginalDescription = "POS 4829 COUNTDOWN AUCKLAND NZ 23/01"
            }
        };

        // Act
        var result = await service.CleanDescriptionsAsync(descriptions);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("not configured");
    }

    [Fact]
    public async Task IsServiceAvailableAsync_WhenNoKernel_ShouldReturnFalse()
    {
        // Arrange
        _kernelFactory.CreateKernelForUserAsync(_testUserId).Returns((Kernel?)null);
        var service = new LlmDescriptionCleaningService(_kernelFactory, _currentUserService, _logger);

        // Act
        var result = await service.IsServiceAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsServiceAvailableAsync_WhenKernelExists_ShouldReturnTrue()
    {
        // Arrange
        var kernel = Kernel.CreateBuilder().Build();
        _kernelFactory.CreateKernelForUserAsync(_testUserId).Returns(kernel);
        var service = new LlmDescriptionCleaningService(_kernelFactory, _currentUserService, _logger);

        // Act
        var result = await service.IsServiceAvailableAsync();

        // Assert
        result.Should().BeTrue();
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
