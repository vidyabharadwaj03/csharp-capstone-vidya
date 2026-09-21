using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using Xunit;

namespace ReservationService.Tests;

public class ReservationServiceIntegrationTests
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
        using var factory = new ReservationServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetActive_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ReservationServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_AsPatron_ReturnsForbidden()
    {
        using var factory = new ReservationServiceWebAppFactory();
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
            context.Reservations.Add(new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = Guid.NewGuid(),
                UserId = userId,
                Status = ReservationStatus.Reserved,
                ReservedAt = DateTime.UtcNow,
                BookTitle = "Clean Code",
                BookAuthor = "Robert Martin"
            });
            await context.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(userId, "Patron"));

        var reservation = await GetSingleReservationAsync(factory);
        var response = await client.PostAsJsonAsync($"/api/reservations/{reservation.ReservationId}/checkout", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CheckoutAndReturn_AsLibrarian_FullFlowSucceeds()
    {
        using var factory = new ReservationServiceWebAppFactory();
        var client = factory.CreateClient();
        var patronId = Guid.NewGuid();
        var librarianId = Guid.NewGuid();

        Guid reservationId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
            var reservation = new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = Guid.NewGuid(),
                UserId = patronId,
                Status = ReservationStatus.Reserved,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                BookTitle = "Clean Code",
                BookAuthor = "Robert Martin"
            };
            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();
            reservationId = reservation.ReservationId;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(librarianId, "Librarian"));

        var checkoutResponse = await client.PostAsJsonAsync($"/api/reservations/{reservationId}/checkout", new { notes = "Good" });
        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);

        var returnResponse = await client.PostAsJsonAsync($"/api/reservations/{reservationId}/return", new { condition = "GOOD" });
        returnResponse.EnsureSuccessStatusCode();

        var returnBody = await returnResponse.Content.ReadFromJsonAsync<ReturnResponse>();
        Assert.Equal(0, returnBody!.LateDays);
    }

    [Fact]
    public async Task GetHistory_AsPatron_ReturnsOwnReservations()
    {
        using var factory = new ReservationServiceWebAppFactory();
        var client = factory.CreateClient();
        var patronId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
            context.Reservations.Add(new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = Guid.NewGuid(),
                UserId = patronId,
                Status = ReservationStatus.Returned,
                ReservedAt = DateTime.UtcNow.AddDays(-5),
                ReturnedAt = DateTime.UtcNow,
                BookTitle = "Clean Code",
                BookAuthor = "Robert Martin"
            });
            await context.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken(patronId, "Patron"));

        var response = await client.GetAsync("/api/reservations/history");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedResult<HistoryEntry>>(ResponseJsonOptions);
        Assert.Single(page!.Content);
    }

    [Fact]
    public async Task GetStatistics_IsAccessibleWithoutAuthentication()
    {
        using var factory = new ReservationServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/reservations/statistics/{Guid.NewGuid()}");

        response.EnsureSuccessStatusCode();
    }

    private static async Task<Reservation> GetSingleReservationAsync(ReservationServiceWebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
        return await context.Reservations.SingleAsync();
    }
}
