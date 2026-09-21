using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UserService.Dtos;
using Xunit;

namespace UserService.Tests;

public class UserServiceIntegrationTests
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        return options;
    }

    [Fact]
    public async Task Health_ReturnsUp()
    {
        using var factory = new UserServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RegisterLoginProfile_FullFlow_Succeeds()
    {
        using var factory = new UserServiceWebAppFactory();
        var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "integration@example.com",
            password = "Str0ng!Pass",
            firstName = "Integ",
            lastName = "Ration",
            phoneNumber = "+1-555-0100"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "integration@example.com",
            password = "Str0ng!Pass"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions);
        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var profileResponse = await client.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profile = await profileResponse.Content.ReadFromJsonAsync<ProfileResponse>(ResponseJsonOptions);
        Assert.Equal("integration@example.com", profile!.Email);
    }

    [Fact]
    public async Task Profile_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new UserServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        using var factory = new UserServiceWebAppFactory();
        var client = factory.CreateClient();

        var payload = new
        {
            email = "dupe@example.com",
            password = "Str0ng!Pass",
            firstName = "Dup",
            lastName = "E",
            phoneNumber = "+1-555-0100"
        };

        await client.PostAsJsonAsync("/api/auth/register", payload);
        var second = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ValidateUser_ForUnknownUser_ReturnsNotFound()
    {
        using var factory = new UserServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}/validate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
