using System.Net;
using ReservationService.Clients;

namespace ReservationService.Tests;

public static class TestClientFactory
{
    public static UserServiceClient CreateUserServiceClient(HttpStatusCode statusCode, string json)
    {
        var handler = FakeHttpMessageHandler.ReturningJson(statusCode, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://userservice") };
        return new UserServiceClient(httpClient);
    }

    public static (CatalogServiceClient Client, List<HttpRequestMessage> Requests) CreateCatalogServiceClient(HttpStatusCode getStatusCode, string getJson)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add(request);
            if (request.Method == HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(getJson, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(getStatusCode)
            {
                Content = new StringContent(getJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://catalogservice") };
        return (new CatalogServiceClient(httpClient), requests);
    }
}
