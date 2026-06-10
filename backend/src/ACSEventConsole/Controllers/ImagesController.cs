using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
public class ImagesController : ControllerBase
{
    private static string PictureDirectory => Path.GetFullPath("D:/Picture");

    [HttpGet("/images")]
    public IActionResult List()
    {
        return Content(ApiJsonSerializer.BuildImageListPage(), "text/html; charset=utf-8", Encoding.UTF8);
    }

    [HttpGet("/images/{**imagePath}")]
    public IActionResult GetImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return ControllerResponses.Text("Invalid image path", "text/plain; charset=utf-8", 400);
        }

        string decodedImagePath = Uri.UnescapeDataString(imagePath);
        string pictureDir = PictureDirectory;
        string fullImagePath = Path.GetFullPath(Path.Combine(pictureDir, decodedImagePath));
        if (!fullImagePath.StartsWith(pictureDir, StringComparison.OrdinalIgnoreCase))
        {
            return ControllerResponses.Text("Access denied", "text/plain; charset=utf-8", 403);
        }

        if (!System.IO.File.Exists(fullImagePath))
        {
            return ControllerResponses.Text("Image not found: " + decodedImagePath, "text/plain; charset=utf-8", 404);
        }

        return PhysicalFile(fullImagePath, ApiJsonSerializer.GetContentType(Path.GetExtension(fullImagePath)));
    }
}
