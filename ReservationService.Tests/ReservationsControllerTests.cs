using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Controllers;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using ReservationService.Services;
using ReservationService.Validation;
using Xunit;

namespace ReservationService.Tests;

public class ReservationsControllerTests
{
    private static ReservationsController CreateController(
        ReservationServiceContext context,
        string userValidationJson,
        string bookJson,
        out List<HttpRequestMessage> catalogRequests,
        HttpStatusCode bookStatusCode = HttpStatusCode.OK)
    {
        var userClient = TestClientFactory.CreateUserServiceClient(HttpStatusCode.OK, userValidationJson);
        var (catalogClient, requests) = TestClientFactory.CreateCatalogServiceClient(bookStatusCode, bookJson);
        catalogRequests = requests;

        var cascadeService = new WaitlistCascadeService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<WaitlistCascadeService>.Instance);
        var validator = new ReturnRequestValidator();

        return new ReservationsController(context, userClient, catalogClient, cascadeService, validator);
    }

    private static void SetUser(ControllerBase controller, Guid userId, string role = "Patron")
    {
        var claims = new[] { new Claim("userId", userId.ToString()), new Claim(ClaimTypes.Role, role) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private const string AvailableBookJson = "{\"bookId\":\"9b1f0000-0000-0000-0000-000000000001\",\"title\":\"Clean Code\",\"author\":\"Robert Martin\",\"totalCopies\":5,\"availableCopies\":5}";
    private const string UnavailableBookJson = "{\"bookId\":\"9b1f0000-0000-0000-0000-000000000001\",\"title\":\"Refactoring\",\"author\":\"Martin Fowler\",\"totalCopies\":3,\"availableCopies\":0}";

    [Fact]
    public async Task Create_WithAvailableBookAndUnderLimit_ReturnsCreated()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{\"activeReservationsCount\":0}", AvailableBookJson, out _);
        var userId = Guid.NewGuid();
        SetUser(controller, userId);

        var result = await controller.Create(new CreateReservationRequest { BookId = Guid.Parse("9b1f0000-0000-0000-0000-000000000001") });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<CreateReservationResponse>(objectResult.Value);
        Assert.Equal(ReservationStatus.Reserved, response.Status);
    }

    [Fact]
    public async Task Create_WhenAtLimit_ReturnsLimitExceeded()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{\"activeReservationsCount\":5}", AvailableBookJson, out _);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Create(new CreateReservationRequest { BookId = Guid.NewGuid() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Create_WhenBookUnavailable_ReturnsBookUnavailable()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{\"activeReservationsCount\":0}", UnavailableBookJson, out _);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Create(new CreateReservationRequest { BookId = Guid.NewGuid() });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_WhenBookNotFound_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{\"activeReservationsCount\":0}", "", out _, HttpStatusCode.NotFound);
        SetUser(controller, Guid.NewGuid());

        var result = await controller.Create(new CreateReservationRequest { BookId = Guid.NewGuid() });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetActive_ComputesDaysUntilExpiryAndDue()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Reservations.Add(new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = userId,
            Status = ReservationStatus.Reserved,
            ReservedAt = now,
            ExpiresAt = now.AddDays(7),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, userId);

        var result = await controller.GetActive();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActiveReservationsResponse>(ok.Value);
        Assert.Equal(1, response.TotalActive);
        Assert.NotNull(response.Reservations[0].DaysUntilExpiry);
    }

    [Fact]
    public async Task Checkout_WhenNotReserved_ReturnsInvalidStatus()
    {
        using var context = TestDbContextFactory.Create();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Checkout_WhenReserved_SetsCheckedOutAndDueDate()
    {
        using var context = TestDbContextFactory.Create();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest { Notes = "Good" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutResponse>(ok.Value);
        Assert.Equal(ReservationStatus.CheckedOut, response.Status);
        Assert.NotNull(response.DueDate);
    }

    [Fact]
    public async Task Checkout_ForMissingReservation_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.Create();
        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Checkout(Guid.NewGuid(), new CheckoutRequest());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Return_WhenNotCheckedOut_ReturnsInvalidStatus()
    {
        using var context = TestDbContextFactory.Create();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Return_WithInvalidCondition_ReturnsValidationError()
    {
        using var context = TestDbContextFactory.Create();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(1),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "TERRIBLE" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Return_OnTime_HasNoLateFeeAndReleasesAvailability()
    {
        using var context = TestDbContextFactory.Create();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = bookId,
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-3),
            CheckedOutAt = DateTime.UtcNow.AddDays(-2),
            DueDate = DateTime.UtcNow.AddDays(5),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out var catalogRequests);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReturnResponse>(ok.Value);
        Assert.Equal(0, response.LateDays);
        Assert.Equal(0m, response.LateFee);
        Assert.Contains(catalogRequests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task Return_WhenLate_CalculatesLateFee()
    {
        using var context = TestDbContextFactory.Create();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-20),
            CheckedOutAt = DateTime.UtcNow.AddDays(-19),
            DueDate = DateTime.UtcNow.AddDays(-3),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        var result = await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "FAIR" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReturnResponse>(ok.Value);
        Assert.True(response.LateDays >= 3);
        Assert.True(response.LateFee >= 3.00m);
    }

    [Fact]
    public async Task Return_WithEligibleWaitlistEntry_DoesNotReleaseAvailability()
    {
        using var context = TestDbContextFactory.Create();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = bookId,
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-3),
            CheckedOutAt = DateTime.UtcNow.AddDays(-2),
            DueDate = DateTime.UtcNow.AddDays(5),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        };
        context.Reservations.Add(reservation);

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = Guid.NewGuid(),
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out var catalogRequests);
        SetUser(controller, Guid.NewGuid(), "Librarian");

        await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        Assert.DoesNotContain(catalogRequests, r => r.Method == HttpMethod.Put);
        Assert.Equal(WaitlistStatus.Notified, context.WaitlistEntries.Single().Status);
    }

    [Fact]
    public async Task GetHistory_ReturnsWasLateFlagAndPagination()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        context.Reservations.Add(new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            UserId = userId,
            Status = ReservationStatus.Returned,
            ReservedAt = DateTime.UtcNow.AddDays(-10),
            DueDate = DateTime.UtcNow.AddDays(-5),
            ReturnedAt = DateTime.UtcNow.AddDays(-3),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);
        SetUser(controller, userId);

        var result = await controller.GetHistory();

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<HistoryEntry>>(ok.Value);
        Assert.Single(page.Content);
        Assert.True(page.Content[0].WasLate);
    }

    [Fact]
    public async Task GetStatistics_CountsActiveAndReturnedReservations()
    {
        using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        context.Reservations.AddRange(
            new Reservation { ReservationId = Guid.NewGuid(), BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Reserved, ReservedAt = DateTime.UtcNow, BookTitle = "A", BookAuthor = "A" },
            new Reservation { ReservationId = Guid.NewGuid(), BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.CheckedOut, ReservedAt = DateTime.UtcNow, BookTitle = "B", BookAuthor = "B" },
            new Reservation { ReservationId = Guid.NewGuid(), BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Returned, ReservedAt = DateTime.UtcNow, BookTitle = "C", BookAuthor = "C" });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "{}", "{}", out _);

        var result = await controller.GetStatistics(userId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StatisticsResponse>(ok.Value);
        Assert.Equal(2, response.ActiveReservations);
        Assert.Equal(1, response.BorrowingHistory);
    }
}
