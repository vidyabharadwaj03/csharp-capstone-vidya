using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public class WaitlistCascadeService
{
    private const int MaxActiveReservations = 5;

    private readonly ReservationServiceContext _context;
    private readonly ILogger<WaitlistCascadeService> _logger;

    public WaitlistCascadeService(ReservationServiceContext context, ILogger<WaitlistCascadeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> OfferCopyToNextEligibleAsync(Guid bookId, string bookTitle, string bookAuthor)
    {
        while (true)
        {
            var next = await _context.WaitlistEntries
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (next is null)
            {
                return false;
            }

            var activeCount = await _context.Reservations.CountAsync(r =>
                r.UserId == next.UserId &&
                (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

            if (activeCount >= MaxActiveReservations)
            {
                next.Status = WaitlistStatus.Expired;
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Waitlist entry {WaitlistId} for book {BookId} expired: patron over the {Limit}-reservation limit",
                    next.WaitlistId, bookId, MaxActiveReservations);
                continue;
            }

            var now = DateTime.UtcNow;
            var reservation = new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = bookId,
                UserId = next.UserId,
                Status = ReservationStatus.Reserved,
                ReservedAt = now,
                ExpiresAt = now.AddDays(7),
                BookTitle = bookTitle,
                BookAuthor = bookAuthor
            };
            _context.Reservations.Add(reservation);

            next.Status = WaitlistStatus.Notified;
            next.NotifiedAt = now;
            next.ClaimDeadline = now.AddHours(48);
            next.ResultingReservationId = reservation.ReservationId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Waitlist entry {WaitlistId} for book {BookId} notified; claim deadline {ClaimDeadline}",
                next.WaitlistId, bookId, next.ClaimDeadline);

            return true;
        }
    }
}
