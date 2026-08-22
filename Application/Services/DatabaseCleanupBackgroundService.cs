using GamePrice.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Application.Services
{
    public sealed class DatabaseCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseCleanupBackgroundService> _logger;

        public DatabaseCleanupBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<DatabaseCleanupBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_configuration.GetValue("DatabaseMaintenance:Enabled", true))
                return;

            var startupDelaySeconds = Math.Clamp(
                _configuration.GetValue("DatabaseMaintenance:StartupDelaySeconds", 30),
                0,
                600);
            var intervalHours = Math.Clamp(
                _configuration.GetValue("DatabaseMaintenance:IntervalHours", 24),
                1,
                168);

            if (startupDelaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
        }

        private async Task CleanupAsync(CancellationToken stoppingToken)
        {
            try
            {
                var now = DateTime.UtcNow;
                var searchCutoff = now.AddDays(-RetentionDays("SearchHistoryDays", 90));
                var loginCutoff = now.AddDays(-RetentionDays("LoginAuditDays", 180));
                var priceCutoff = now.AddDays(-RetentionDays("PriceSnapshotDays", 365));
                var inactiveOfferCutoff = now.AddDays(-RetentionDays("InactiveOfferDays", 30));

                await using var scope = _scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<GamePriceDbContext>();

                var searches = await database.SearchHistory
                    .Where(item => item.SearchedAt < searchCutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                var loginAudits = await database.LoginAudits
                    .Where(item => item.OccurredAt < loginCutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                var priceSnapshots = await database.PriceSnapshots
                    .Where(item => item.ObservedAt < priceCutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                var inactiveOffers = await database.Offers
                    .Where(item => !item.IsActive && item.ObservedAt < inactiveOfferCutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                var orphanGames = await database.Games
                    .Where(item => item.UpdatedAt < inactiveOfferCutoff
                        && !item.Offers.Any()
                        && !item.WishlistAlerts.Any())
                    .ExecuteDeleteAsync(stoppingToken);

                await database.Database.ExecuteSqlRawAsync("PRAGMA optimize;", stoppingToken);

                _logger.LogInformation(
                    "Limpeza do SQLite concluida: {Searches} buscas, {LoginAudits} auditorias, "
                    + "{PriceSnapshots} snapshots, {InactiveOffers} ofertas inativas e {OrphanGames} jogos orfaos removidos",
                    searches,
                    loginAudits,
                    priceSnapshots,
                    inactiveOffers,
                    orphanGames);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Falha na limpeza periodica do SQLite");
            }
        }

        private int RetentionDays(string key, int defaultValue) => Math.Clamp(
            _configuration.GetValue($"DatabaseMaintenance:{key}", defaultValue),
            7,
            3650);
    }
}
