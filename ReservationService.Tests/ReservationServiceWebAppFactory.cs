using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReservationService.Clients;
using ReservationService.Data;

namespace ReservationService.Tests;

public class ReservationServiceWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var databaseName = Guid.NewGuid().ToString();

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ReservationServiceContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ReservationServiceContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.AddHttpClient<UserServiceClient>(client => client.BaseAddress = new Uri("http://userservice"))
                .ConfigurePrimaryHttpMessageHandler(() => FakeHttpMessageHandler.ReturningJson(
                    System.Net.HttpStatusCode.OK, "{\"activeReservationsCount\":0}"));

            services.AddHttpClient<CatalogServiceClient>(client => client.BaseAddress = new Uri("http://catalogservice"))
                .ConfigurePrimaryHttpMessageHandler(() => FakeHttpMessageHandler.ReturningJson(
                    System.Net.HttpStatusCode.OK, "{\"bookId\":\"9b1f0000-0000-0000-0000-000000000001\",\"title\":\"Stub\",\"author\":\"Stub\",\"totalCopies\":1,\"availableCopies\":1}"));
        });
    }
}
