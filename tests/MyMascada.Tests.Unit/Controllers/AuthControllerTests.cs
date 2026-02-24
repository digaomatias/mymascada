using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.Commands;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Application.Features.Authentication.Queries;
using MyMascada.Domain.Entities;
using MyMascada.WebAPI.Controllers;
using NSubstitute;
using System.Security.Claims;

namespace MyMascada.Tests.Unit.Controllers;

public class AuthControllerTests
{
    private readonly IMediator _mediator;
    private readonly IAuthenticationService _authService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<AppOptions> _appOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly IUserAiSettingsRepository _aiSettingsRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserFinancialProfileRepository _financialProfileRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _authService = Substitute.For<IAuthenticationService>();
        _dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
        _userRepository = Substitute.For<IUserRepository>();
        _appOptions = Options.Create(new AppOptions { FrontendUrl = "http://localhost:3000" });
        _environment = Substitute.For<IWebHostEnvironment>();
        _environment.EnvironmentName.Returns("Development");
        _aiSettingsRepository = Substitute.For<IUserAiSettingsRepository>();
        _configuration = Substitute.For<IConfiguration>();
        _financialProfileRepository = Substitute.For<IUserFinancialProfileRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _controller = new AuthController(_mediator, _authService, _dataProtectionProvider, _userRepository, _appOptions, _environment, _aiSettingsRepository, _configuration, _financialProfileRepository, _accountRepository);

        // Provide a default HttpContext so methods that access Request.Headers don't throw
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Register_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "TestPass123!",
            ConfirmPassword = "TestPass123!",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1234567890",
            Currency = "USD",
            TimeZone = "America/New_York"
        };

        var expectedResponse = new AuthenticationResponse
        {
            IsSuccess = true,
            Token = "sample-jwt-token",
            User = new UserDto
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                UserName = request.UserName,
                FirstName = request.FirstName,
                LastName = request.LastName,
                FullName = $"{request.FirstName} {request.LastName}",
                Currency = request.Currency,
                TimeZone = request.TimeZone
            }
        };

        _mediator.Send(Arg.Any<RegisterCommand>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthenticationResponse>().Subject;
        response.IsSuccess.Should().BeTrue();
        response.Token.Should().Be("sample-jwt-token");
        response.User.Email.Should().Be(request.Email);

        await _mediator.Received(1).Send(Arg.Is<RegisterCommand>(cmd =>
            cmd.Email == request.Email &&
            cmd.UserName == request.UserName &&
            cmd.Password == request.Password &&
            cmd.ConfirmPassword == request.ConfirmPassword &&
            cmd.FirstName == request.FirstName &&
            cmd.LastName == request.LastName &&
            cmd.PhoneNumber == request.PhoneNumber &&
            cmd.Currency == request.Currency &&
            cmd.TimeZone == request.TimeZone));
    }

    [Fact]
    public async Task Register_WithInvalidRequest_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "invalid-email",
            Password = "weak",
            ConfirmPassword = "different"
        };

        var expectedResponse = new AuthenticationResponse
        {
            IsSuccess = false,
            Errors = new List<string> { "Invalid email format", "Password too weak", "Passwords do not match" }
        };

        _mediator.Send(Arg.Any<RegisterCommand>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<AuthenticationResponse>().Subject;
        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOk()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUserName = "test@example.com",
            Password = "TestPass123!",
            RememberMe = true
        };

        var expectedResponse = new AuthenticationResponse
        {
            IsSuccess = true,
            Token = "sample-jwt-token",
            User = new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                UserName = "testuser",
                FirstName = "Test",
                LastName = "User",
                FullName = "Test User",
                Currency = "USD",
                TimeZone = "UTC"
            }
        };

        _mediator.Send(Arg.Any<LoginQuery>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthenticationResponse>().Subject;
        response.IsSuccess.Should().BeTrue();
        response.Token.Should().Be("sample-jwt-token");

        await _mediator.Received(1).Send(Arg.Is<LoginQuery>(query =>
            query.EmailOrUserName == request.EmailOrUserName &&
            query.Password == request.Password &&
            query.RememberMe == request.RememberMe));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            EmailOrUserName = "test@example.com",
            Password = "WrongPassword"
        };

        var expectedResponse = new AuthenticationResponse
        {
            IsSuccess = false,
            Errors = new List<string> { "Invalid email or password" }
        };

        _mediator.Send(Arg.Any<LoginQuery>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<AuthenticationResponse>().Subject;
        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().Contain("Invalid email or password");
    }

    [Fact]
    public void Health_ShouldReturnHealthyStatus()
    {
        // Act
        var result = _controller.Health();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeAssignableTo<object>().Subject;
        
        // Verify the anonymous object has the expected properties
        var healthResponse = value.GetType().GetProperty("Status")?.GetValue(value);
        var timestamp = value.GetType().GetProperty("Timestamp")?.GetValue(value);
        
        healthResponse.Should().Be("Healthy");
        timestamp.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCurrentUser_WithValidClaims_ShouldReturnUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "test@example.com"),
            new(ClaimTypes.GivenName, "Test"),
            new(ClaimTypes.Surname, "User"),
            new(ClaimTypes.Name, "testuser")
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

        // Mock user repository to return user
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            Currency = "USD",
            TimeZone = "UTC",
            Locale = "en"
        };
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var userDto = okResult.Value.Should().BeOfType<UserDto>().Subject;

        userDto.Id.Should().Be(userId);
        userDto.Email.Should().Be("test@example.com");
        userDto.UserName.Should().Be("testuser");
        userDto.FirstName.Should().Be("Test");
        userDto.LastName.Should().Be("User");
        userDto.FullName.Should().Be("Test User");
        userDto.Currency.Should().Be("USD");
        userDto.TimeZone.Should().Be("UTC");
        userDto.Locale.Should().Be("en");
    }

    [Fact]
    public async Task GetCurrentUser_WithMissingUserIdClaim_ShouldReturnUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "test@example.com")
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

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidUserIdClaim_ShouldReturnUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "invalid-guid"),
            new(ClaimTypes.Email, "test@example.com")
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

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCurrentUser_WithPartialClaims_ShouldReturnUserDtoWithDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "test@example.com")
            // Missing other claims
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

        // Mock user repository to return user with minimal data
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            UserName = null,
            FirstName = null,
            LastName = null,
            Currency = null,
            TimeZone = null,
            Locale = null
        };
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var userDto = okResult.Value.Should().BeOfType<UserDto>().Subject;

        userDto.Id.Should().Be(userId);
        userDto.Email.Should().Be("test@example.com");
        userDto.UserName.Should().Be("");
        userDto.FirstName.Should().Be("");
        userDto.LastName.Should().Be("");
        userDto.FullName.Should().Be("");
        userDto.Currency.Should().Be("NZD");
        userDto.TimeZone.Should().Be("UTC");
        userDto.Locale.Should().Be("en");
    }

    [Fact]
    public async Task GetCurrentUser_WithUserNotInDatabase_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
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

        // Mock user repository to return null (user not found)
        _userRepository.GetByIdAsync(userId).Returns((User?)null);

        // Act
        var result = await _controller.GetCurrentUser();

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}