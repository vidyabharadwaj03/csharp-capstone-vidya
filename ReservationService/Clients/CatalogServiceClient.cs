using System.Net;
using System.Net.Http.Json;

namespace ReservationService.Clients;

public class CatalogServiceClient
{
    private readonly HttpClient _httpClient;

    public CatalogServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BookInfo?> GetBookAsync(Guid bookId)
    {
        var response = await _httpClient.GetAsync($"/api/catalog/books/{bookId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BookInfo>(InterServiceJson.Options);
    }

    public async Task UpdateAvailabilityAsync(Guid bookId, int delta)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/catalog/books/{bookId}/availability", new { delta }, InterServiceJson.Options);
        response.EnsureSuccessStatusCode();
    }
}
