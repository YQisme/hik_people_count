using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
[Route("api/limit-count")]
public class LimitCountController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        RuntimeConfig config = RuntimeConfig.LoadDefault();
        return Content(ApiJsonSerializer.SerializeLimitCount(config.LimitCount), "application/json", Encoding.UTF8);
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        string contentType = Request.ContentType ?? string.Empty;
        int limitCount = ApiJsonSerializer.ReadLimitCountFromRequest(body, contentType);
        if (limitCount <= 0)
        {
            return ControllerResponses.Json("{\"message\":\"limitCount must be greater than 0\"}", 400);
        }

        if (!ApiJsonSerializer.TrySaveLimitCount(limitCount, out string errorMessage))
        {
            return ControllerResponses.Json($"{{\"message\":\"{ApiJsonSerializer.Escape(errorMessage)}\"}}", 500);
        }

        return Content(ApiJsonSerializer.SerializeLimitCount(limitCount), "application/json", Encoding.UTF8);
    }
}
