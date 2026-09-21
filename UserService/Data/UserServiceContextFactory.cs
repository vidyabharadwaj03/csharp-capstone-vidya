using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UserService.Data;

public class UserServiceContextFactory : IDesignTimeDbContextFactory<UserServiceContext>
{
    public UserServiceContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<UserServiceContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("UserDb"));

        return new UserServiceContext(optionsBuilder.Options);
    }
}
