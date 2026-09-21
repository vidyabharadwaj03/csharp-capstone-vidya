using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UserService.Controllers;
using UserService.Dtos;
using UserService.Options;
using UserService.Services;
using UserService.Validation;
using Xunit;

namespace UserService.Tests;

public class AuthControllerTests
{
    private static AuthController CreateController(Data.UserServiceContext context)
    {
        var passwordHasher = new PasswordHasher();
        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Secret = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiresInSeconds = 86400
        });
        var tokenService = new JwtTokenService(jwtOptions);
        var validator = new RegisterRequestValidator();

        return new AuthController(context, passwordHasher, tokenService, validator, NullLogger<AuthController>.Instance);
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreated()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        var result = await controller.Register(new RegisterRequest
        {
            Email = "new.user@example.com",
            Password = "Str0ng!Pass",
            FirstName = "New",
            LastName = "User",
            PhoneNumber = "+1-555-0100"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<RegisterResponse>(objectResult.Value);
        Assert.Equal("new.user@example.com", response.Email);
        Assert.Equal(Models.Role.Patron, response.Role);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsValidationError()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        var result = await controller.Register(new RegisterRequest
        {
            Email = "weak@example.com",
            Password = "weak",
            FirstName = "Weak",
            LastName = "Pass",
            PhoneNumber = "+1-555-0100"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("VALIDATION_ERROR", error.Error);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsValidationError()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        var request = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Password = "Str0ng!Pass",
            FirstName = "First",
            LastName = "User",
            PhoneNumber = "+1-555-0100"
        };

        await controller.Register(request);
        var secondResult = await controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(secondResult);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("VALIDATION_ERROR", error.Error);
        Assert.Equal("Email already exists", error.Message);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        await controller.Register(new RegisterRequest
        {
            Email = "login@example.com",
            Password = "Str0ng!Pass",
            FirstName = "Log",
            LastName = "In",
            PhoneNumber = "+1-555-0100"
        });

        var result = await controller.Login(new LoginRequest
        {
            Email = "login@example.com",
            Password = "Str0ng!Pass"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal("Bearer", response.TokenType);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        await controller.Register(new RegisterRequest
        {
            Email = "badlogin@example.com",
            Password = "Str0ng!Pass",
            FirstName = "Bad",
            LastName = "Login",
            PhoneNumber = "+1-555-0100"
        });

        var result = await controller.Login(new LoginRequest
        {
            Email = "badlogin@example.com",
            Password = "WrongPassword1!"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("AUTHENTICATION_FAILED", error.Error);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context);

        var result = await controller.Login(new LoginRequest
        {
            Email = "doesnotexist@example.com",
            Password = "Whatever1!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
