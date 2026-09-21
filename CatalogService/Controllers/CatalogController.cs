using CatalogService.Common;
using CatalogService.Data;
using CatalogService.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly CatalogServiceContext _context;

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "author", "publicationyear"
    };

    public CatalogController(CatalogServiceContext context)
    {
        _context = context;
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string sortBy = "title",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] string? query = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? isbn = null,
        [FromQuery] bool availableOnly = false)
    {
        var books = _context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            books = books.Where(b => b.Title.ToLower().Contains(term) || b.Author.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            books = books.Where(b => b.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            books = books.Where(b => b.Isbn == isbn);
        }

        if (availableOnly)
        {
            books = books.Where(b => b.AvailableCopies > 0);
        }

        var sortField = AllowedSortFields.Contains(sortBy) ? sortBy.ToLowerInvariant() : "title";
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        books = sortField switch
        {
            "author" => descending ? books.OrderByDescending(b => b.Author) : books.OrderBy(b => b.Author),
            "publicationyear" => descending ? books.OrderByDescending(b => b.PublicationYear) : books.OrderBy(b => b.PublicationYear),
            _ => descending ? books.OrderByDescending(b => b.Title) : books.OrderBy(b => b.Title)
        };

        var totalElements = await books.CountAsync();
        var totalPages = size > 0 ? (int)Math.Ceiling(totalElements / (double)size) : 0;

        var content = await books
            .Skip(page * size)
            .Take(size)
            .Select(b => new BookSummaryResponse
            {
                BookId = b.BookId,
                Isbn = b.Isbn,
                Title = b.Title,
                Author = b.Author,
                Genre = b.Genre,
                PublicationYear = b.PublicationYear,
                Description = b.Description,
                TotalCopies = b.TotalCopies,
                AvailableCopies = b.AvailableCopies,
                Status = BookStatusCalculator.Compute(b.AvailableCopies)
            })
            .ToListAsync();

        return Ok(new PagedResult<BookSummaryResponse>
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("books/{bookId:guid}")]
    public async Task<IActionResult> GetBookById(Guid bookId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {bookId}"
            });
        }

        return Ok(new BookDetailResponse
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = BookStatusCalculator.Compute(book.AvailableCopies),
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }

    [HttpPut("books/{bookId:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid bookId, [FromBody] UpdateAvailabilityRequest request)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {bookId}"
            });
        }

        var updated = book.AvailableCopies + request.Delta;
        if (updated < 0 || updated > book.TotalCopies)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Resulting available copies would be out of range"
            });
        }

        book.AvailableCopies = updated;
        await _context.SaveChangesAsync();

        return Ok(new BookDetailResponse
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = BookStatusCalculator.Compute(book.AvailableCopies),
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }
}
