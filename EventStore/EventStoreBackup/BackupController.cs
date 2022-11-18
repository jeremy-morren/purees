using Microsoft.AspNetCore.Mvc;

namespace EventStoreBackup;

[ApiController]
[Route("/admin/[controller]")]
public class BackupController : Controller
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    [HttpPost]
    public IActionResult Index([FromQuery] CompressionType? compression) => new BackupActionResult(compression);
}