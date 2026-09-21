using System.Text.Json;

namespace ReservationService.Clients;

public static class InterServiceJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
