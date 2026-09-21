using System.Net.Http.Json;
using System.Text.Json;

namespace UserService.Services;

public class ReservationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ReservationServiceClient> _logger;

    public ReservationServiceClient(HttpClient httpClient, ILogger<ReservationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReservationStatistics> GetStatisticsAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/reservations/statistics/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reservation Service returned {StatusCode} for statistics lookup", response.StatusCode);
                return new ReservationStatistics();
            }

            var statistics = await response.Content.ReadFromJsonAsync<ReservationStatistics>(JsonOptions);
            return statistics ?? new ReservationStatistics();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Reservation Service is unavailable");
            return new ReservationStatistics();
        }
    }
}
