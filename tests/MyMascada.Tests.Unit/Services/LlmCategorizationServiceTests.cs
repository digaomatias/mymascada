using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Services;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Services;

public class LlmCategorizationServiceTests
{
    private readonly IUserAiKernelFactory _kernelFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LlmCategorizationService> _logger;
    private readonly Guid _testUserId = Guid.NewGuid();

    public LlmCategorizationServiceTests()
    {
        _kernelFactory = Substitute.For<IUserAiKernelFactory>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<LlmCategorizationService>>();

        _currentUserService.GetUserId().Returns(_testUserId);
    }

    [Fact]
    public async Task CategorizeTransactionsAsync_WhenNoKernel_ShouldReturnFailure()
    {
        // Arrange
        _kernelFactory.CreateKernelForUserAsync(_testUserId).Returns((Kernel?)null);
        var service = new LlmCategorizationService(_kernelFactory, _currentUserService, _logger);

        var transactions = new List<Transaction>
        {
            new Transaction { Id = 1, Description = "Test", Amount = -10m }
        };
        var categories = new List<Category>
        {
            new Category { Id = 1, Name = "Test" }
        };

        // Act
        var result = await service.CategorizeTransactionsAsync(transactions, categories);

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
        var service = new LlmCategorizationService(_kernelFactory, _currentUserService, _logger);

        // Act
        var result = await service.IsServiceAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendPromptAsync_WhenNoKernel_ShouldThrowInvalidOperation()
    {
        // Arrange
        _kernelFactory.CreateKernelForUserAsync(_testUserId).Returns((Kernel?)null);
        var service = new LlmCategorizationService(_kernelFactory, _currentUserService, _logger);

        // Act & Assert
        Func<Task> act = () => service.SendPromptAsync("test");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }
}
