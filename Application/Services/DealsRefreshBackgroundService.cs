using GamePrice.Api.Application.Interfaces;

namespace GamePrice.Api.Application.Services
{
    public sealed class DealsRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DealsRefreshBackgroundService> _logger;

        public DealsRefreshBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<DealsRefreshBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var configuredMinutes = _configuration.GetValue<int>("Feeds:DealsRefreshMinutes", 15);
            var refreshInterval = TimeSpan.FromMinutes(Math.Clamp(configuredMinutes, 5, 1440));
            var configuredDelaySeconds = _configuration.GetValue<int>("Feeds:StartupDelaySeconds", 10);
            var startupDelay = TimeSpan.FromSeconds(Math.Clamp(configuredDelaySeconds, 0, 120));
            var retryInterval = TimeSpan.FromMinutes(1);

            if (startupDelay > TimeSpan.Zero)
                await Task.Delay(startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var refreshed = await RefreshDealsAsync(stoppingToken);
                await Task.Delay(refreshed ? refreshInterval : retryInterval, stoppingToken);
            }
        }

        private async Task<bool> RefreshDealsAsync(CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested)
                return false;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var scraperService = scope.ServiceProvider.GetRequiredService<IScraperService>();
                var deals = await scraperService.GetTopDealsAsync(forceRefresh: true);

                _logger.LogInformation(
                    "Feed de ofertas atualizado em segundo plano com {Count} item(ns)",
                    deals.Count);
                return deals.Count > 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao atualizar o feed de ofertas em segundo plano");
                return false;
            }
        }
    }
}
