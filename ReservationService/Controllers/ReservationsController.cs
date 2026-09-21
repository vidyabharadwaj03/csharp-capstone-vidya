using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Clients;
using ReservationService.Common;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using ReservationService.Services;
using FluentValidation;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private const int MaxActiveReservations = 5;
    private const int ReservationExpiryDays = 7;
    private const int CheckoutPeriodDays = 14;
    private const decimal LateFeePerDay = 1.00m;

    private readonly ReservationServiceContext _context;
    private readonly UserServiceClient _userServiceClient;
    private readonly CatalogServiceClient _catalogServiceClient;
    private readonly WaitlistCascadeService _cascadeService;
    private readonly IValidator<ReturnRequest> _returnValidator;

    public ReservationsController(
        ReservationServiceContext context,
        UserServiceClient userServiceClient,
        CatalogServiceClient catalogServiceClient,
        WaitlistCascadeService cascadeService,
        IValidator<ReturnRequest> returnValidator)
    {
        _context = context;
        _userServiceClient = userServiceClient;
        _catalogServiceClient = catalogServiceClient;
        _cascadeService = cascadeService;
        _returnValidator = returnValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
    {
        var userId = User.GetUserId();

        UserValidationResult? validation;
        try
        {
            validation = await _userServiceClient.ValidateUserAsync(userId);
        }
        catch (UserSuspendedException)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Your account is suspended and cannot make reservations"
            });
        }

        if (validation is null)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "User could not be validated"
            });
        }

        if (validation.ActiveReservationsCount >= MaxActiveReservations)
        {
            return BadRequest(new
            {
                error = "RESERVATION_LIMIT_EXCEEDED",
                message = "You have reached the maximum of 5 active reservations",
                currentReservations = validation.ActiveReservationsCount
            });
        }

        var book = await _catalogServiceClient.GetBookAsync(request.BookId);
        if (book is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {request.BookId}"
            });
        }

        if (book.AvailableCopies <= 0)
        {
            return BadRequest(new
            {
                error = "BOOK_UNAVAILABLE",
                message = "No copies available for reservation",
                availableCopies = book.AvailableCopies
            });
        }

        await _catalogServiceClient.UpdateAvailabilityAsync(request.BookId, -1);

        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = request.BookId,
            UserId = userId,
            Status = ReservationStatus.Reserved,
            ReservedAt = now,
            ExpiresAt = now.AddDays(ReservationExpiryDays),
            BookTitle = book.Title,
            BookAuthor = book.Author
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return StatusCode(201, new CreateReservationResponse
        {
            ReservationId = reservation.ReservationId,
            BookId = reservation.BookId,
            UserId = reservation.UserId,
            BookTitle = reservation.BookTitle,
            Status = reservation.Status,
            ReservedAt = reservation.ReservedAt,
            ExpiresAt = reservation.ExpiresAt,
            Message = "Book reserved successfully. Please pick up within 7 days."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        var reservations = await _context.Reservations
            .Where(r => r.UserId == userId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut))
            .ToListAsync();

        var entries = reservations.Select(r => new ActiveReservationEntry
        {
            ReservationId = r.ReservationId,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            Status = r.Status,
            ReservedAt = r.ReservedAt,
            ExpiresAt = r.ExpiresAt,
            DaysUntilExpiry = r.Status == ReservationStatus.Reserved && r.ExpiresAt.HasValue
                ? (int?)Math.Ceiling((r.ExpiresAt.Value - now).TotalDays)
                : null,
            CheckedOutAt = r.CheckedOutAt,
            DueDate = r.DueDate,
            DaysUntilDue = r.Status == ReservationStatus.CheckedOut && r.DueDate.HasValue
                ? (int?)Math.Ceiling((r.DueDate.Value - now).TotalDays)
                : null
        }).ToList();

        return Ok(new ActiveReservationsResponse
        {
            Reservations = entries,
            TotalActive = entries.Count
        });
    }

    [HttpPost("{reservationId:guid}/checkout")]
    [Authorize(Roles = "Librarian")]
    public async Task<IActionResult> Checkout(Guid reservationId, [FromBody] CheckoutRequest request)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = "Reservation not found"
            });
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only checkout reservations with RESERVED status",
                currentStatus = reservation.Status.ToString().ToUpperInvariant()
            });
        }

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = now;
        reservation.DueDate = now.AddDays(CheckoutPeriodDays);
        reservation.Notes = request.Notes;

        await _context.SaveChangesAsync();

        return Ok(new CheckoutResponse
        {
            ReservationId = reservation.ReservationId,
            Status = reservation.Status,
            CheckedOutAt = reservation.CheckedOutAt,
            DueDate = reservation.DueDate,
            Message = $"Book checked out successfully. Due date: {reservation.DueDate:MMMM d, yyyy}"
        });
    }

    [HttpPost("{reservationId:guid}/return")]
    [Authorize(Roles = "Librarian")]
    public async Task<IActionResult> Return(Guid reservationId, [FromBody] ReturnRequest request)
    {
        var validationResult = await _returnValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = validationResult.Errors.First().ErrorMessage
            });
        }

        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = "Reservation not found"
            });
        }

        if (reservation.Status != ReservationStatus.CheckedOut)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only return books with CHECKED_OUT status",
                currentStatus = reservation.Status.ToString().ToUpperInvariant()
            });
        }

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.Returned;
        reservation.ReturnedAt = now;
        reservation.Condition = Enum.Parse<BookCondition>(request.Condition, ignoreCase: true);
        reservation.Notes = request.Notes;

        var lateDays = 0;
        if (reservation.DueDate.HasValue && now > reservation.DueDate.Value)
        {
            lateDays = (int)Math.Ceiling((now - reservation.DueDate.Value).TotalDays);
        }

        reservation.LateDays = lateDays;
        reservation.LateFee = lateDays * LateFeePerDay;

        await _context.SaveChangesAsync();

        var claimed = await _cascadeService.OfferCopyToNextEligibleAsync(reservation.BookId, reservation.BookTitle, reservation.BookAuthor);
        if (!claimed)
        {
            await _catalogServiceClient.UpdateAvailabilityAsync(reservation.BookId, 1);
        }

        var message = lateDays > 0
            ? $"Book returned. Late fee of ${reservation.LateFee:F2} applied to account."
            : "Book returned successfully";

        return Ok(new ReturnResponse
        {
            ReservationId = reservation.ReservationId,
            ReturnedAt = reservation.ReturnedAt,
            DueDate = reservation.DueDate,
            LateDays = lateDays,
            LateFee = reservation.LateFee ?? 0m,
            Message = message
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        var userId = User.GetUserId();

        var query = _context.Reservations.Where(r => r.UserId == userId);
        var totalElements = await query.CountAsync();
        var totalPages = size > 0 ? (int)Math.Ceiling(totalElements / (double)size) : 0;

        var reservations = await query
            .OrderByDescending(r => r.ReturnedAt ?? r.ReservedAt)
            .Skip(page * size)
            .Take(size)
            .ToListAsync();

        var content = reservations.Select(r => new HistoryEntry
        {
            ReservationId = r.ReservationId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            ReservedAt = r.ReservedAt,
            CheckedOutAt = r.CheckedOutAt,
            ReturnedAt = r.ReturnedAt,
            DueDate = r.DueDate,
            Status = r.Status,
            WasLate = r.ReturnedAt.HasValue && r.DueDate.HasValue && r.ReturnedAt.Value > r.DueDate.Value
        }).ToList();

        return Ok(new PagedResult<HistoryEntry>
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("statistics/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatistics(Guid userId)
    {
        var activeReservations = await _context.Reservations.CountAsync(r =>
            r.UserId == userId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

        var borrowingHistory = await _context.Reservations.CountAsync(r =>
            r.UserId == userId && r.Status == ReservationStatus.Returned);

        return Ok(new StatisticsResponse
        {
            UserId = userId,
            ActiveReservations = activeReservations,
            BorrowingHistory = borrowingHistory
        });
    }
}
