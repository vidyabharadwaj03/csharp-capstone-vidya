using CatalogService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly CatalogServiceContext _context;

    public HealthController(CatalogServiceContext context)
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
            service = "CatalogService",
            database = databaseName,
            migrations = migrationsCount
        });
    }
}
