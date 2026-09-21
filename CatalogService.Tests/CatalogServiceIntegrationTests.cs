using System.Net;
using System.Net.Http.Json;
using CatalogService.Dtos;
using Xunit;

namespace CatalogService.Tests;

public class CatalogServiceIntegrationTests
{
    [Fact]
    public async Task Health_ReturnsUp()
    {
        using var factory = new CatalogServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetBooks_ReturnsSeededCatalog()
    {
        using var factory = new CatalogServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/catalog/books");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedResult<BookSummaryResponse>>();
        Assert.NotNull(page);
        Assert.True(page!.TotalElements > 0);
    }

    [Fact]
    public async Task GetBookById_ForMissingBook_ReturnsNotFound()
    {
        using var factory = new CatalogServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/catalog/books/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAvailability_ForMissingBook_ReturnsNotFound()
    {
        using var factory = new CatalogServiceWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/catalog/books/{Guid.NewGuid()}/availability", new { delta = -1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
