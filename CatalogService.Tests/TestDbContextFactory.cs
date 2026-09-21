using CatalogService.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Tests;

public static class TestDbContextFactory
{
    public static CatalogServiceContext Create()
    {
        var options = new DbContextOptionsBuilder<CatalogServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CatalogServiceContext(options);
    }
}
