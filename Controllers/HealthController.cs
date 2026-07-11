using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GamePrice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private static readonly DateTime _startTime = DateTime.UtcNow;

        [HttpGet]
        public IActionResult Get()
        {
            var memoryUsed = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var process = Process.GetCurrentProcess();
            var systemMemory = process.PrivateMemorySize64 / (1024.0 * 1024.0);
            var uptime = DateTime.UtcNow - _startTime;

            return Ok(new
            {
                status = "Healthy",
                service = "GamePrice.Api",
                uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                uptimeMs = uptime.TotalMilliseconds,
                memory = new
                {
                    allocatedMb = Math.Round(memoryUsed, 2),
                    systemPrivateMb = Math.Round(systemMemory, 2)
                },
                database = "Online (In-Memory Repository)",
                crawlers = new[] { "Steam", "Epic Games", "GOG", "Nuuvem", "Xbox", "PlayStation", "Nintendo" },
                timestamp = DateTime.UtcNow
            });
        }
    }
}