using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
[Route("api/channels")]
public class ChannelsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetChannels()
    {
        return Content(ApiJsonSerializer.SerializeChannels(), "application/json", Encoding.UTF8);
    }

    [HttpGet("overview")]
    public IActionResult GetOverview()
    {
        var overview = new ChannelOverviewBuilder(ACSEventMultiDeviceService.SharedEventStore.Snapshot()).Build();
        return Content(ApiJsonSerializer.SerializeChannelOverview(overview), "application/json", Encoding.UTF8);
    }
}
