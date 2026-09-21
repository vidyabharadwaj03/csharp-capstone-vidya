using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Clients;
using ReservationService.Common;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly ReservationServiceContext _context;
    private readonly CatalogServiceClient _catalogServiceClient;
    private readonly WaitlistCascadeService _cascadeService;

    public WaitlistController(
        ReservationServiceContext context,
        CatalogServiceClient catalogServiceClient,
        WaitlistCascadeService cascadeService)
    {
        _context = context;
        _catalogServiceClient = catalogServiceClient;
        _cascadeService = cascadeService;
    }

    [HttpPost]
    public async Task<IActionResult> Join([FromBody] JoinWaitlistRequest request)
    {
        var userId = User.GetUserId();

        var book = await _catalogServiceClient.GetBookAsync(request.BookId);
        if (book is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {request.BookId}"
            });
        }

        if (book.AvailableCopies > 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "BOOK_AVAILABLE",
                Message = "This book currently has available copies - reserve it directly instead of joining the waitlist"
            });
        }

        var alreadyWaiting = await _context.WaitlistEntries.AnyAsync(w =>
            w.BookId == request.BookId && w.UserId == userId && w.Status == WaitlistStatus.Waiting);
        if (alreadyWaiting)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "ALREADY_WAITLISTED",
                Message = "You are already on the waitlist for this book"
            });
        }

        var now = DateTime.UtcNow;
        var entry = new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = request.BookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now,
            BookTitle = book.Title,
            BookAuthor = book.Author
        };

        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync();

        var position = await _context.WaitlistEntries.CountAsync(w =>
            w.BookId == request.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt);

        return StatusCode(201, new JoinWaitlistResponse
        {
            WaitlistId = entry.WaitlistId,
            BookId = entry.BookId,
            BookTitle = entry.BookTitle,
            Status = entry.Status,
            JoinedAt = entry.JoinedAt,
            Position = position
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyEntries()
    {
        var userId = User.GetUserId();

        var entries = await _context.WaitlistEntries
            .Where(w => w.UserId == userId && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .ToListAsync();

        var result = new List<WaitlistEntry>();
        foreach (var entry in entries)
        {
            int? position = null;
            if (entry.Status == WaitlistStatus.Waiting)
            {
                position = await _context.WaitlistEntries.CountAsync(w =>
                    w.BookId == entry.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt);
            }

            result.Add(new WaitlistEntry
            {
                WaitlistId = entry.WaitlistId,
                BookId = entry.BookId,
                BookTitle = entry.BookTitle,
                BookAuthor = entry.BookAuthor,
                Status = entry.Status,
                JoinedAt = entry.JoinedAt,
                Position = position,
                NotifiedAt = entry.NotifiedAt,
                ClaimDeadline = entry.ClaimDeadline
            });
        }

        return Ok(new WaitlistEntriesResponse { Entries = result });
    }

    [HttpDelete("{waitlistId:guid}")]
    public async Task<IActionResult> Leave(Guid waitlistId)
    {
        var userId = User.GetUserId();

        var entry = await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.WaitlistId == waitlistId && w.UserId == userId);
        if (entry is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = "Waitlist entry not found"
            });
        }

        var wasNotified = entry.Status == WaitlistStatus.Notified;
        entry.Status = WaitlistStatus.Cancelled;
        await _context.SaveChangesAsync();

        if (wasNotified)
        {
            var claimed = await _cascadeService.OfferCopyToNextEligibleAsync(entry.BookId, entry.BookTitle, entry.BookAuthor);
            if (!claimed)
            {
                await _catalogServiceClient.UpdateAvailabilityAsync(entry.BookId, 1);
            }
        }

        return Ok(new LeaveWaitlistResponse
        {
            WaitlistId = entry.WaitlistId,
            Status = entry.Status,
            Message = "You have been removed from the waitlist"
        });
    }
}
