using CatalogService.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CatalogService.Tests;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsUpStatus()
    {
        using var context = TestDbContextFactory.Create();
        var controller = new HealthController(context);

        var result = controller.Get();

        Assert.IsType<OkObjectResult>(result);
    }
}
