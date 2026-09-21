using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;

namespace UserService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly UserServiceContext _context;

    public HealthController(UserServiceContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var isRelational = _context.Database.IsRelational();
        var databaseName = isRelational ? _context.Database.GetDbConnection().Database : "in-memory";
        var migrationsCount = isRelational ? _context.Database.GetAppliedMigrations().Count() : 0;

        return Ok(new
        {
            status = "UP",
            service = "UserService",
            database = databaseName,
            migrations = migrationsCount
        });
    }
}
