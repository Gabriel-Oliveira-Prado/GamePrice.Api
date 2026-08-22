using System.Diagnostics;
using GamePrice.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly GamePriceDbContext _database;

        public HealthController(GamePriceDbContext database)
        {
            _database = database;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var memoryUsed = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var process = Process.GetCurrentProcess();
            var systemMemory = process.PrivateMemorySize64 / (1024.0 * 1024.0);
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            if (uptime < TimeSpan.Zero)
                uptime = TimeSpan.Zero;

            var databaseOnline = await _database.Database.CanConnectAsync(cancellationToken);
            var database = new Dictionary<string, object?>
            {
                ["status"] = databaseOnline ? "Online" : "Offline",
                ["provider"] = "SQLite"
            };

            if (databaseOnline)
            {
                database["users"] = await _database.Users.CountAsync(cancellationToken);
                database["games"] = await _database.Games.CountAsync(cancellationToken);
                database["stores"] = await _database.Stores.CountAsync(cancellationToken);
                database["offers"] = await _database.Offers.CountAsync(cancellationToken);
                database["priceSnapshots"] = await _database.PriceSnapshots.CountAsync(cancellationToken);
                database["searches"] = await _database.SearchHistory.CountAsync(cancellationToken);
                database["loginAttempts"] = await _database.LoginAudits.CountAsync(cancellationToken);
            }

            var response = new
            {
                status = databaseOnline ? "Healthy" : "Degraded",
                service = "GamePrice.Api",
                uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                uptimeMs = uptime.TotalMilliseconds,
                memory = new
                {
                    allocatedMb = Math.Round(memoryUsed, 2),
                    systemPrivateMb = Math.Round(systemMemory, 2)
                },
                database,
                crawlers = new[] { "Steam", "Epic Games", "GOG", "Nuuvem", "Xbox", "PlayStation", "Nintendo" },
                timestamp = DateTime.UtcNow
            };

            return databaseOnline ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }
}
