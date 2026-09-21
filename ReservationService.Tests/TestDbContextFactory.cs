using Microsoft.EntityFrameworkCore;
using ReservationService.Data;

namespace ReservationService.Tests;

public static class TestDbContextFactory
{
    public static ReservationServiceContext Create()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ReservationServiceContext(options);
    }
}
