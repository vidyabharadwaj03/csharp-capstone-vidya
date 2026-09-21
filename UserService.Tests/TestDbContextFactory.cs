using Microsoft.EntityFrameworkCore;
using UserService.Data;

namespace UserService.Tests;

public static class TestDbContextFactory
{
    public static UserServiceContext Create()
    {
        var options = new DbContextOptionsBuilder<UserServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new UserServiceContext(options);
    }
}
