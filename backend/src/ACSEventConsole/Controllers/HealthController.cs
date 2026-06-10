using Microsoft.AspNetCore.Mvc;

namespace ACSEventConsole.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get()
    {
        return Content("{\"status\":\"ok\"}", "application/json");
    }
}
