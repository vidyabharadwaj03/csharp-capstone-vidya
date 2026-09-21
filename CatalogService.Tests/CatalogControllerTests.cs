using CatalogService.Controllers;
using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CatalogService.Tests;

public class CatalogControllerTests
{
    private static async Task<CatalogServiceContext> SeedAsync()
    {
        var context = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;

        context.Books.AddRange(
            new Book
            {
                BookId = Guid.NewGuid(),
                Isbn = "111",
                Title = "Clean Code",
                Author = "Robert Martin",
                Genre = "Technology",
                PublicationYear = 2008,
                TotalCopies = 5,
                AvailableCopies = 5,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Book
            {
                BookId = Guid.NewGuid(),
                Isbn = "222",
                Title = "Refactoring",
                Author = "Martin Fowler",
                Genre = "Technology",
                PublicationYear = 2018,
                TotalCopies = 3,
                AvailableCopies = 0,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Book
            {
                BookId = Guid.NewGuid(),
                Isbn = "333",
                Title = "1984",
                Author = "George Orwell",
                Genre = "Fiction",
                PublicationYear = 1949,
                TotalCopies = 6,
                AvailableCopies = 6,
                CreatedAt = now,
                UpdatedAt = now
            });

        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task GetBooks_WithDefaults_ReturnsAllSortedByTitleAscending()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBooks();

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Equal(3, page.TotalElements);
        Assert.Equal(new[] { "1984", "Clean Code", "Refactoring" }, page.Content.Select(b => b.Title));
    }

    [Fact]
    public async Task GetBooks_WithDescendingSort_ReversesOrder()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBooks(sortOrder: "desc");

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Equal(new[] { "Refactoring", "Clean Code", "1984" }, page.Content.Select(b => b.Title));
    }

    [Fact]
    public async Task GetBooks_WithAvailableOnly_ExcludesZeroCopyBooks()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBooks(availableOnly: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.DoesNotContain(page.Content, b => b.Title == "Refactoring");
    }

    [Fact]
    public async Task GetBooks_WithGenreFilter_ReturnsMatchingOnly()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBooks(genre: "Fiction");

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Single(page.Content);
        Assert.Equal("1984", page.Content[0].Title);
    }

    [Fact]
    public async Task GetBooks_WithSearchQuery_MatchesTitleOrAuthor()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBooks(query: "fowler");

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Single(page.Content);
        Assert.Equal("Refactoring", page.Content[0].Title);
    }

    [Fact]
    public async Task GetBooks_WithNoMatches_ReturnsEmptyContent()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBooks(query: "nonexistent-book-xyz");

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Empty(page.Content);
    }

    [Fact]
    public async Task GetBookById_WhenFound_ReturnsDetails()
    {
        using var context = await SeedAsync();
        var book = context.Books.First();
        var controller = new CatalogController(context);

        var result = await controller.GetBookById(book.BookId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<BookDetailResponse>(ok.Value);
        Assert.Equal(book.BookId, detail.BookId);
    }

    [Fact]
    public async Task GetBookById_WhenMissing_ReturnsNotFound()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.GetBookById(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateAvailability_WithNegativeDelta_DecrementsCopies()
    {
        using var context = await SeedAsync();
        var book = context.Books.First(b => b.Title == "Clean Code");
        var controller = new CatalogController(context);

        var result = await controller.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = -1 });

        var ok = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<BookDetailResponse>(ok.Value);
        Assert.Equal(4, detail.AvailableCopies);
    }

    [Fact]
    public async Task UpdateAvailability_BelowZero_ReturnsBadRequest()
    {
        using var context = await SeedAsync();
        var book = context.Books.First(b => b.Title == "Refactoring");
        var controller = new CatalogController(context);

        var result = await controller.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = -1 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateAvailability_AboveTotalCopies_ReturnsBadRequest()
    {
        using var context = await SeedAsync();
        var book = context.Books.First(b => b.Title == "Clean Code");
        var controller = new CatalogController(context);

        var result = await controller.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = 10 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateAvailability_ForMissingBook_ReturnsNotFound()
    {
        using var context = await SeedAsync();
        var controller = new CatalogController(context);

        var result = await controller.UpdateAvailability(Guid.NewGuid(), new UpdateAvailabilityRequest { Delta = 1 });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
