using Microsoft.EntityFrameworkCore;
using UserService.Models;
using UserService.Services;

namespace UserService.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(UserServiceContext context, PasswordHasher passwordHasher)
    {
        if (await context.Users.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var librarian = new User
        {
            UserId = Guid.NewGuid(),
            Email = "librarian@library.local",
            PasswordHash = passwordHasher.Hash("Librarian123!"),
            FirstName = "Head",
            LastName = "Librarian",
            PhoneNumber = "+1-555-0100",
            Role = Role.Librarian,
            MembershipStatus = MembershipStatus.Active,
            MemberSince = now
        };

        await context.Users.AddAsync(librarian);
        await context.SaveChangesAsync();
    }
}
