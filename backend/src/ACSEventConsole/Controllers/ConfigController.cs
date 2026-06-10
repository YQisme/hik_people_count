using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace ACSEventConsole.Controllers;

[ApiController]
public class ConfigController : ControllerBase
{
    [HttpGet("/config")]
    public IActionResult GetConfig()
    {
        string configPath = ConfigPaths.DeviceConfigPath;
        if (!System.IO.File.Exists(configPath))
        {
            return ControllerResponses.Text("DeviceConfig.json not found", "text/plain; charset=utf-8", 404);
        }

        return Content(System.IO.File.ReadAllText(configPath, Encoding.UTF8), "application/json", Encoding.UTF8);
    }

    [HttpPost("/config")]
    public async Task<IActionResult> SaveConfig()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        string contentType = Request.ContentType ?? string.Empty;
        string jsonPayload = body;

        if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var form = ApiJsonSerializer.ParseForm(body);
            form.TryGetValue("json", out jsonPayload);
        }

        try
        {
            var document = JsonSerializer.Deserialize<DeviceConfigDocument>(
                jsonPayload ?? string.Empty,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document == null)
            {
                throw new InvalidOperationException("配置内容为空");
            }

            DeviceConfigStore.Save(ConfigPaths.DeviceConfigPath, document);
            if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/config/edit?saved=1");
            }

            return Content("OK", "text/plain", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return ControllerResponses.Text("Invalid JSON: " + ex.Message, "text/plain; charset=utf-8", 400);
        }
    }

    [HttpGet("/config/edit")]
    public IActionResult EditPage([FromQuery] bool saved)
    {
        string configPath = ConfigPaths.DeviceConfigPath;
        string json = string.Empty;
        try
        {
            if (System.IO.File.Exists(configPath))
            {
                json = System.IO.File.ReadAllText(configPath, Encoding.UTF8);
            }
        }
        catch
        {
        }

        bool savedFlag = saved || string.Equals(Request.Query["saved"], "1", StringComparison.OrdinalIgnoreCase);
        string page = ApiJsonSerializer.BuildEditorPage(json, savedFlag);
        return Content(page, "text/html; charset=utf-8", Encoding.UTF8);
    }
}
