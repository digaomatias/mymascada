using Microsoft.Extensions.Hosting;
using MyMascada.WebAPI.Extensions;
using NSubstitute;

namespace MyMascada.Tests.Unit.Extensions;

public class HostEnvironmentExtensionsTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Debug", true)]
    [InlineData("Prod-QA", true)]
    [InlineData("debug", true)]
    [InlineData("prod-qa", true)]
    [InlineData("Production", false)]
    public void IsLocalDevelopment_ReturnsExpectedValue(string environmentName, bool expected)
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        // Act
        var result = environment.IsLocalDevelopment();

        // Assert
        result.Should().Be(expected);
    }
}
