using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Controllers;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests;

public class WaitlistControllerTests
{
    private const string AvailableBookJson = "{\"bookId\":\"9b1f0000-0000-0000-0000-000000000001\",\"title\":\"Clean Code\",\"author\":\"Robert Martin\",\"totalCopies\":5,\"availableCopies\":5}";
    private const string UnavailableBookJson = "{\"bookId\":\"9b1f0000-0000-0000-0000-000000000001\",\"title\":\"Refactoring\",\"author\":\"Martin Fowler\",\"totalCopies\":3,\"availableCopies\":0}";

    private static WaitlistController CreateController(
        ReservationServiceContext context,
        string bookJson,
        out List<HttpRequestMessage> catalogRequests,
        HttpStatusCode bookStatusCode = HttpStatusCode.OK)
    {
        var (catalogClient, requests) = TestClientFactory.CreateCatalogServiceClient(bookStatusCode, bookJson);
        catalogRequests = requests;
        var cascadeService = new WaitlistCascadeService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<WaitlistCascadeService>.Instance);

        return new WaitlistController(context, catalogClient, cascadeService);
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
    public async Task Join_WhenBookUnavailable_ReturnsCreatedWithPosition()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, UnavailableBookJson, out _);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Join(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<JoinWaitlistResponse>(objectResult.Value);
        Assert.Equal(1, response.Position);
        Assert.Equal(WaitlistStatus.Waiting, response.Status);
    }

    [Fact]
    public async Task Join_WhenBookAvailable_ReturnsBookAvailableError()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, AvailableBookJson, out _);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Join(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("BOOK_AVAILABLE", error.Error);
    }

    [Fact]
    public async Task Join_WhenAlreadyWaitlisted_ReturnsAlreadyWaitlistedError()
    {
        using var context = TestDbContextFactory.Create();
        var bookId = Guid.Parse("9b1f0000-0000-0000-0000-000000000001");
        var userId = Guid.NewGuid();

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = "Refactoring",
            BookAuthor = "Martin Fowler"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, UnavailableBookJson, out _);
        SetUser(controller, userId);

        var result = await controller.Join(new JoinWaitlistRequest { BookId = bookId });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("ALREADY_WAITLISTED", error.Error);
    }

    [Fact]
    public async Task Join_WhenBookNotFound_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "", out _, HttpStatusCode.NotFound);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Join(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetMyEntries_ReturnsPositionForWaitingAndDeadlineForNotified()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now,
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = userId,
            Status = WaitlistStatus.Notified,
            JoinedAt = now.AddDays(-3),
            NotifiedAt = now,
            ClaimDeadline = now.AddHours(48),
            BookTitle = "Refactoring",
            BookAuthor = "Martin Fowler"
        });

        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", out _);
        SetUser(controller, userId);

        var result = await controller.GetMyEntries();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<WaitlistEntriesResponse>(ok.Value);
        Assert.Equal(2, response.Entries.Count);
        Assert.Contains(response.Entries, e => e.Status == WaitlistStatus.Waiting && e.Position == 1);
        Assert.Contains(response.Entries, e => e.Status == WaitlistStatus.Notified && e.ClaimDeadline != null);
    }

    [Fact]
    public async Task Leave_ForMissingEntry_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{}", out _);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Leave(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Leave_ForWaitingEntry_CancelsWithoutCallingCatalog()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var entry = new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.WaitlistEntries.Add(entry);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", out var catalogRequests);
        SetUser(controller, userId);

        var result = await controller.Leave(entry.WaitlistId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LeaveWaitlistResponse>(ok.Value);
        Assert.Equal(WaitlistStatus.Cancelled, response.Status);
        Assert.Empty(catalogRequests);
    }

    [Fact]
    public async Task Leave_ForNotifiedEntryWithEmptyQueue_ReleasesCopyBackToCatalog()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var entry = new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = userId,
            Status = WaitlistStatus.Notified,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            NotifiedAt = DateTime.UtcNow,
            ClaimDeadline = DateTime.UtcNow.AddHours(48),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.WaitlistEntries.Add(entry);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", out var catalogRequests);
        SetUser(controller, userId);

        await controller.Leave(entry.WaitlistId);

        Assert.Contains(catalogRequests, r => r.Method == HttpMethod.Put);
    }
}
