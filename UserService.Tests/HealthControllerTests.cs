using Microsoft.AspNetCore.Mvc;
using UserService.Controllers;
using Xunit;

namespace UserService.Tests;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsUpStatus()
    {
        using var context = TestDbContextFactory.Create();
        var controller = new HealthController(context);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
