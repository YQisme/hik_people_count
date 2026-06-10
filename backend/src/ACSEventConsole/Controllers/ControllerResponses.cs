using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

internal static class ControllerResponses
{
    public static ContentResult Text(string content, string contentType, int statusCode = 200)
    {
        return new ContentResult
        {
            Content = content,
            ContentType = contentType,
            StatusCode = statusCode
        };
    }

    public static ContentResult Json(string content, int statusCode = 200)
        => Text(content, "application/json; charset=utf-8", statusCode);
}
