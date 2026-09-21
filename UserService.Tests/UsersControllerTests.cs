using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UserService.Controllers;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;
using UserService.Services;
using Xunit;

namespace UserService.Tests;

public class UsersControllerTests
{
    private static UsersController CreateController(UserServiceContext context, string statisticsJson)
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, statisticsJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://reservationservice") };
        var client = new ReservationServiceClient(httpClient, NullLogger<ReservationServiceClient>.Instance);

        return new UsersController(context, client);
    }

    private static void SetUser(ControllerBase controller, Guid userId)
    {
        var claims = new[] { new Claim("userId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetProfile_ForExistingUser_ReturnsProfileWithStatistics()
    {
        using var context = TestDbContextFactory.Create();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "profile@example.com",
            PasswordHash = "hash",
            FirstName = "Pro",
            LastName = "File",
            PhoneNumber = "+1-555-0100",
            Role = Role.Patron,
            MembershipStatus = MembershipStatus.Active,
            MemberSince = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{\"activeReservations\":2,\"borrowingHistory\":5}");
        SetUser(controller, user.UserId);

        var result = await controller.GetProfile();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(2, response.ActiveReservations);
        Assert.Equal(5, response.BorrowingHistory);
    }

    [Fact]
    public async Task GetProfile_ForMissingUser_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{}");
        SetUser(controller, Guid.NewGuid());

        var result = await controller.GetProfile();

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ValidateUser_ForActiveUser_ReturnsValidationResponse()
    {
        using var context = TestDbContextFactory.Create();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "active@example.com",
            PasswordHash = "hash",
            FirstName = "Act",
            LastName = "Ive",
            PhoneNumber = "+1-555-0100",
            Role = Role.Patron,
            MembershipStatus = MembershipStatus.Active
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{\"activeReservations\":1,\"borrowingHistory\":0}");

        var result = await controller.ValidateUser(user.UserId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserValidationResponse>(ok.Value);
        Assert.Equal(1, response.ActiveReservationsCount);
    }

    [Fact]
    public async Task ValidateUser_ForSuspendedUser_ReturnsBadRequest()
    {
        using var context = TestDbContextFactory.Create();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "suspended@example.com",
            PasswordHash = "hash",
            FirstName = "Sus",
            LastName = "Pended",
            PhoneNumber = "+1-555-0100",
            Role = Role.Patron,
            MembershipStatus = MembershipStatus.Suspended
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}");

        var result = await controller.ValidateUser(user.UserId);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ValidateUser_ForMissingUser_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{}");

        var result = await controller.ValidateUser(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
