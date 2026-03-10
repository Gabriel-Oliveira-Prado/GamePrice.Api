using System.Net.Http.Json;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GamePrice.Api.Application.Services
{
    public class ScraperService : IScraperService
    {
        private readonly HttpClient _http;
        private readonly ICacheService _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScraperService> _logger;

        public ScraperService(
            HttpClient http,
            ICacheService cache,
            IConfiguration configuration,
            ILogger<ScraperService> logger)
        {
            _http = http;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GamePriceDto?> GetGamePriceAsync(string gameName)
        {
            var cacheKey = $"game_price_{gameName.ToLowerInvariant().Trim()}";

            // Verifica se já está em cache
            var cached = await _cache.GetAsync<GamePriceDto>(cacheKey);
            if (cached is not null)
            {
                _logger.LogInformation("Cache HIT para o jogo: {GameName}", gameName);
                return cached;
            }

            _logger.LogInformation("Cache MISS para o jogo: {GameName}. Consultando scraper...", gameName);

            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var pythonUrl = $"{baseUrl.TrimEnd('/')}/scrape?url={Uri.EscapeDataString(gameName)}";

            _logger.LogInformation("Chamando scraper Python em: {Url}", pythonUrl);

            var data = await _http.GetFromJsonAsync<GamePriceDto>(pythonUrl);

            if (data is not null)
            {
                // Cachear resultado — tempo configurável via appsettings
                var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                await _cache.SetAsync(cacheKey, data, TimeSpan.FromMinutes(expirationMinutes));
                _logger.LogInformation("Resultado cacheado por {Minutes} minutos para: {GameName}", expirationMinutes, gameName);
            }

            return data;
        }
    }
}
