using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
public class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        int webPort = RuntimeConfig.LoadDefault().WebPort;
        return Content(ApiJsonSerializer.BuildHomePage(webPort), "text/html; charset=utf-8", Encoding.UTF8);
    }
}
