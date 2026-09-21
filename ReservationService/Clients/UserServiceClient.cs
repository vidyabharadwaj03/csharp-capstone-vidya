using System.Net;
using System.Net.Http.Json;

namespace ReservationService.Clients;

public class UserServiceClient
{
    private readonly HttpClient _httpClient;

    public UserServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserValidationResult?> ValidateUserAsync(Guid userId)
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}/validate");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new UserSuspendedException();
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserValidationResult>(InterServiceJson.Options);
    }
}
