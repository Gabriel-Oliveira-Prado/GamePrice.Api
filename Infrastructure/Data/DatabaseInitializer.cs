using GamePrice.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Infrastructure.Data
{
    public static class DatabaseInitializer
    {
        private static readonly (string Name, string Slug, string Website)[] Stores =
        {
            ("Steam", "steam", "https://store.steampowered.com"),
            ("Epic Games", "epic-games", "https://store.epicgames.com"),
            ("GOG", "gog", "https://www.gog.com"),
            ("Nuuvem", "nuuvem", "https://www.nuuvem.com"),
            ("itch.io", "itch-io", "https://itch.io"),
            ("Xbox", "xbox", "https://www.xbox.com"),
            ("PlayStation", "playstation", "https://store.playstation.com"),
            ("Nintendo", "nintendo", "https://www.nintendo.com")
        };

        public static async Task InitializeAsync(
            GamePriceDbContext database,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            await database.Database.EnsureCreatedAsync(cancellationToken);
            await database.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
            await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            await database.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);

            var existingSlugs = await database.Stores
                .Select(store => store.Slug)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var store in Stores.Where(store => !existingSlugs.Contains(store.Slug)))
            {
                database.Stores.Add(new StoreModel
                {
                    Name = store.Name,
                    Slug = store.Slug,
                    WebsiteUrl = store.Website,
                    CreatedAt = now
                });
            }

            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("SQLite inicializado com {StoreCount} lojas cadastradas", await database.Stores.CountAsync(cancellationToken));
        }
    }
}
