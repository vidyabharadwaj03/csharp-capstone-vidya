using UserService.Dtos;
using UserService.Validation;
using Xunit;

namespace UserService.Tests;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_WithAllValidFields_Passes()
    {
        var result = _validator.Validate(new RegisterRequest
        {
            Email = "valid@example.com",
            Password = "Str0ng!Pass",
            FirstName = "Val",
            LastName = "Id",
            PhoneNumber = "+1-555-0100"
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSpecialChar1")]
    public void Validate_WithInvalidPassword_Fails(string password)
    {
        var result = _validator.Validate(new RegisterRequest
        {
            Email = "valid@example.com",
            Password = password,
            FirstName = "Val",
            LastName = "Id",
            PhoneNumber = "+1-555-0100"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithInvalidEmail_Fails()
    {
        var result = _validator.Validate(new RegisterRequest
        {
            Email = "not-an-email",
            Password = "Str0ng!Pass",
            FirstName = "Val",
            LastName = "Id",
            PhoneNumber = "+1-555-0100"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithInvalidPhoneNumber_Fails()
    {
        var result = _validator.Validate(new RegisterRequest
        {
            Email = "valid@example.com",
            Password = "Str0ng!Pass",
            FirstName = "Val",
            LastName = "Id",
            PhoneNumber = "not-a-phone-number!!"
        });

        Assert.False(result.IsValid);
    }
}
