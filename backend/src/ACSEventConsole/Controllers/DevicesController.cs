using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] string channelId)
    {
        return Content(ApiJsonSerializer.SerializeDevices(channelId), "application/json", Encoding.UTF8);
    }
}
