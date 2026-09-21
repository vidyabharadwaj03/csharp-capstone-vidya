using Microsoft.Extensions.Logging.Abstractions;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests;

public class WaitlistCascadeServiceTests
{
    private static WaitlistCascadeService CreateService(ReservationServiceContext context)
    {
        return new WaitlistCascadeService(context, NullLogger<WaitlistCascadeService>.Instance);
    }

    [Fact]
    public async Task OfferCopy_WithEmptyQueue_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var claimed = await service.OfferCopyToNextEligibleAsync(Guid.NewGuid(), "Some Book", "Some Author");

        Assert.False(claimed);
    }

    [Fact]
    public async Task OfferCopy_WithEligibleWaitingEntry_NotifiesAndCreatesReservation()
    {
        using var context = TestDbContextFactory.Create();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var claimed = await service.OfferCopyToNextEligibleAsync(bookId, "Clean Code", "Robert Martin");

        Assert.True(claimed);

        var entry = context.WaitlistEntries.Single();
        Assert.Equal(WaitlistStatus.Notified, entry.Status);
        Assert.NotNull(entry.NotifiedAt);
        Assert.NotNull(entry.ClaimDeadline);
        Assert.NotNull(entry.ResultingReservationId);

        var reservation = context.Reservations.Single();
        Assert.Equal(userId, reservation.UserId);
        Assert.Equal(bookId, reservation.BookId);
        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
        Assert.Equal(entry.ResultingReservationId, reservation.ReservationId);
    }

    [Fact]
    public async Task OfferCopy_SkipsPatronOverLimit_AndTriesNextInQueue()
    {
        using var context = TestDbContextFactory.Create();
        var bookId = Guid.NewGuid();
        var overLimitUser = Guid.NewGuid();
        var eligibleUser = Guid.NewGuid();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            context.Reservations.Add(new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = Guid.NewGuid(),
                UserId = overLimitUser,
                Status = ReservationStatus.Reserved,
                ReservedAt = now,
                BookTitle = "Other",
                BookAuthor = "Other"
            });
        }

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = overLimitUser,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now.AddDays(-2),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = eligibleUser,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now.AddDays(-1),
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });

        await context.SaveChangesAsync();

        var service = CreateService(context);
        var claimed = await service.OfferCopyToNextEligibleAsync(bookId, "Clean Code", "Robert Martin");

        Assert.True(claimed);

        var overLimitEntry = context.WaitlistEntries.Single(w => w.UserId == overLimitUser);
        Assert.Equal(WaitlistStatus.Expired, overLimitEntry.Status);

        var eligibleEntry = context.WaitlistEntries.Single(w => w.UserId == eligibleUser);
        Assert.Equal(WaitlistStatus.Notified, eligibleEntry.Status);
    }

    [Fact]
    public async Task OfferCopy_WhenEveryoneOverLimit_ExpiresAllAndReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        var bookId = Guid.NewGuid();
        var overLimitUser = Guid.NewGuid();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            context.Reservations.Add(new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = Guid.NewGuid(),
                UserId = overLimitUser,
                Status = ReservationStatus.CheckedOut,
                ReservedAt = now,
                BookTitle = "Other",
                BookAuthor = "Other"
            });
        }

        context.WaitlistEntries.Add(new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = bookId,
            UserId = overLimitUser,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now,
            BookTitle = "Clean Code",
            BookAuthor = "Robert Martin"
        });

        await context.SaveChangesAsync();

        var service = CreateService(context);
        var claimed = await service.OfferCopyToNextEligibleAsync(bookId, "Clean Code", "Robert Martin");

        Assert.False(claimed);
        Assert.Equal(WaitlistStatus.Expired, context.WaitlistEntries.Single().Status);
    }
}
