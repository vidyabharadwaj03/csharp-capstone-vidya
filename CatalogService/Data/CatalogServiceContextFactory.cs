using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CatalogService.Data;

public class CatalogServiceContextFactory : IDesignTimeDbContextFactory<CatalogServiceContext>
{
    public CatalogServiceContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CatalogServiceContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("CatalogDb"));

        return new CatalogServiceContext(optionsBuilder.Options);
    }
}
