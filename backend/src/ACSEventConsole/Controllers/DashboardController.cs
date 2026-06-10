using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
public class DashboardController : ControllerBase
{
    [HttpGet("/api/dashboard")]
    [HttpGet("/dashboard-data")]
    public IActionResult GetDashboard([FromQuery] string channelId)
    {
        var payload = new DashboardDataBuilder(ACSEventMultiDeviceService.SharedEventStore.Snapshot(), channelId).Build();
        return Content(ApiJsonSerializer.SerializeDashboard(payload), "application/json", Encoding.UTF8);
    }

    [HttpGet("/api/dashboard/stream")]
    public async Task StreamDashboard([FromQuery] string channelId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.ContentType = "text/event-stream; charset=utf-8";

        var store = ACSEventMultiDeviceService.SharedEventStore;
        long lastRevision = -1;
        DateTime lastHeartbeatUtc = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                long revision = store.Revision;
                if (revision != lastRevision)
                {
                    DashboardPayload payload = new DashboardDataBuilder(store.Snapshot(), channelId).Build();
                    string json = ApiJsonSerializer.SerializeDashboard(payload);
                    await Response.WriteAsync($"event: dashboard\ndata: {json}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    lastRevision = revision;
                    lastHeartbeatUtc = DateTime.UtcNow;
                }
                else if ((DateTime.UtcNow - lastHeartbeatUtc).TotalSeconds >= 15)
                {
                    await Response.WriteAsync(": heartbeat\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    lastHeartbeatUtc = DateTime.UtcNow;
                }

                await Task.Delay(250, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
