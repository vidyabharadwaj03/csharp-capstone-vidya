using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;

namespace ReservationService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ReservationServiceContext _context;

    public HealthController(ReservationServiceContext context)
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
            service = "ReservationService",
            database = databaseName,
            migrations = migrationsCount
        });
    }
}
