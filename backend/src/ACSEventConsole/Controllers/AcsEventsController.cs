using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
[Route("api/acs-events")]
public class AcsEventsController : ControllerBase
{
    [HttpPost("history")]
    public async Task<IActionResult> QueryHistory()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        string deviceIP = ApiJsonSerializer.ExtractStringValue(body, "deviceIP");
        string startTime = ApiJsonSerializer.ExtractStringValue(body, "startTime");
        string endTime = ApiJsonSerializer.ExtractStringValue(body, "endTime");
        if (string.IsNullOrEmpty(deviceIP) || string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
        {
            return ControllerResponses.Json("{\"message\":\"deviceIP, startTime and endTime are required\"}", 400);
        }

        int major = body.IndexOf("\"major\"", StringComparison.OrdinalIgnoreCase) >= 0
            ? ApiJsonSerializer.ExtractIntValue(body, "major")
            : 5;
        int minor = body.IndexOf("\"minor\"", StringComparison.OrdinalIgnoreCase) >= 0
            ? ApiJsonSerializer.ExtractIntValue(body, "minor")
            : 0;

        var query = new AcsEventHistoryQuery
        {
            DeviceIP = deviceIP,
            StartTime = startTime,
            EndTime = endTime,
            Major = major,
            Minor = minor,
            MaxResults = ApiJsonSerializer.ExtractIntValue(body, "maxResults") > 0
                ? ApiJsonSerializer.ExtractIntValue(body, "maxResults")
                : 30,
            SearchResultPosition = ApiJsonSerializer.ExtractIntValue(body, "searchResultPosition"),
            FetchAll = !string.Equals(ApiJsonSerializer.ExtractStringValue(body, "fetchAll"), "false", StringComparison.OrdinalIgnoreCase),
            MaxTotal = ApiJsonSerializer.ExtractIntValue(body, "maxTotal") > 0
                ? ApiJsonSerializer.ExtractIntValue(body, "maxTotal")
                : 500
        };

        AcsEventHistoryResult history = AcsEventIsapiClient.QueryHistory(query);
        if (!string.IsNullOrEmpty(history.ErrorMessage))
        {
            return ControllerResponses.Json(ApiJsonSerializer.SerializeHistoryError(history.ErrorMessage), 502);
        }

        return Content(ApiJsonSerializer.SerializeHistoryResult(history), "application/json", Encoding.UTF8);
    }

    [HttpGet("picture")]
    public IActionResult GetPicture(
        [FromQuery] string deviceIP,
        [FromQuery] string source,
        [FromQuery] string serialNo,
        [FromQuery] string employeeNo)
    {
        deviceIP = (deviceIP ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(deviceIP))
        {
            return ControllerResponses.Text("deviceIP is required", "text/plain; charset=utf-8", 400);
        }

        byte[] imageBytes = AcsEventImageResolver.DownloadPicture(
            deviceIP,
            (source ?? string.Empty).Trim(),
            (serialNo ?? string.Empty).Trim(),
            (employeeNo ?? string.Empty).Trim());

        if (imageBytes == null || imageBytes.Length == 0)
        {
            return ControllerResponses.Text("Image not found", "text/plain; charset=utf-8", 404);
        }

        string contentType = imageBytes.Length >= 2 && imageBytes[0] == 0x89 && imageBytes[1] == 0x50
            ? "image/png"
            : "image/jpeg";
        return File(imageBytes, contentType);
    }
}
