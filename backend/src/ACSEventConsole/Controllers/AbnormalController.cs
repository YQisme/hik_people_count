using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
[Route("api/abnormal")]
public class AbnormalController : ControllerBase
{
    [HttpPost("close")]
    public async Task<IActionResult> Close()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        string messageId = ApiJsonSerializer.ExtractStringValue(body, "id");
        if (string.IsNullOrEmpty(messageId))
        {
            return ControllerResponses.Json("{\"message\":\"id is required\"}", 400);
        }

        bool handled = AbnormalMessageState.MarkHandled(messageId);
        if (!handled)
        {
            return ControllerResponses.Json("{\"message\":\"message not found\"}", 404);
        }

        return Content(ApiJsonSerializer.SerializeAbnormalCloseResult(messageId, true), "application/json", Encoding.UTF8);
    }
}
