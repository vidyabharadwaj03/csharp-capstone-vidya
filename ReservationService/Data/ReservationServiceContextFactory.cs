using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ReservationService.Data;

public class ReservationServiceContextFactory : IDesignTimeDbContextFactory<ReservationServiceContext>
{
    public ReservationServiceContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ReservationServiceContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("ReservationDb"));

        return new ReservationServiceContext(optionsBuilder.Options);
    }
}
