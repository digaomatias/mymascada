using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Commands;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Tests.Unit.Commands;

public class InitiateAkahuConnectionCommandHandlerTests
{
    [Fact]
    public async Task Handle_HostedOAuthMode_ReturnsAuthorizationUrlAndSkipsCredentialLookup()
    {
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(new BankProviderModeResolution(
            "akahu",
            "hosted_oauth",
            new[]
            {
                new BankProviderAuthModeInfo
                {
                    ModeId = "hosted_oauth",
                    DisplayName = "MyMascada OAuth",
                    RequiresUserCredentials = false
                }
            }));

        akahuApiClient.GetAuthorizationUrl(Arg.Any<string>(), "neo@example.com")
            .Returns("https://next.oauth.akahu.nz/?client_id=app_token_xxx");

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient,
            credentialRepository,
            bankConnectionRepository,
            encryptionService,
            modeResolver,
            logger);

        var result = await handler.Handle(new InitiateAkahuConnectionCommand(Guid.NewGuid(), "neo@example.com"), CancellationToken.None);

        result.IsPersonalAppMode.Should().BeFalse();
        result.RequiresCredentials.Should().BeFalse();
        result.AuthorizationUrl.Should().StartWith("https://next.oauth.akahu.nz/");
        result.State.Should().NotBeNullOrWhiteSpace();

        await credentialRepository.DidNotReceive().GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersonalMode_NoCredentials_ReturnsRequiresCredentials()
    {
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(new BankProviderModeResolution(
            "akahu",
            "personal_tokens",
            new[]
            {
                new BankProviderAuthModeInfo
                {
                    ModeId = "personal_tokens",
                    DisplayName = "Personal tokens",
                    RequiresUserCredentials = true
                }
            }));

        credentialRepository.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MyMascada.Domain.Entities.AkahuUserCredential?)null);

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient,
            credentialRepository,
            bankConnectionRepository,
            encryptionService,
            modeResolver,
            logger);

        var result = await handler.Handle(new InitiateAkahuConnectionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsPersonalAppMode.Should().BeTrue();
        result.RequiresCredentials.Should().BeTrue();
        result.AuthorizationUrl.Should().BeNull();
    }
}
