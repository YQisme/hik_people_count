using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
public class EventsController : ControllerBase
{
    [HttpGet("/events")]
    public IActionResult GetEvents()
    {
        return Content(
            ApiJsonSerializer.SerializeEvents(ACSEventMultiDeviceService.SharedEventStore.Snapshot()),
            "application/json",
            Encoding.UTF8);
    }
}
