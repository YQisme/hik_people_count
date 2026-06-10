using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ACSEventConsole.Controllers;

[ApiController]
public class EmployeesController : ControllerBase
{
    [HttpGet("/api/employee")]
    [HttpGet("/api/employee/search")]
    public IActionResult Search([FromQuery] string q)
    {
        string keyword = (q ?? string.Empty).Trim();
        return Content(
            ApiJsonSerializer.SerializeEmployees(ApiJsonSerializer.FilterEmployees(keyword)),
            "application/json",
            Encoding.UTF8);
    }
}
