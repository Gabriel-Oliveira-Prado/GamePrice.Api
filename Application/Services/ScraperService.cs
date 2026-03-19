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

            var results = await _http.GetFromJsonAsync<Dictionary<string, PythonStoreResultDto>>(pythonUrl);

            if (results == null || results.Count == 0)
                return null;

            GamePriceDto? bestGame = null;
            decimal lowestPrice = decimal.MaxValue;

            foreach (var kvp in results)
            {
                var storeName = kvp.Key;
                var storeData = kvp.Value;

                if (string.IsNullOrEmpty(storeData.Nome) || storeData.Erro != null)
                    continue;

                decimal currentPrice = ParsePrice(storeData.PrecoAtual);
                
                if (currentPrice < lowestPrice)
                {
                    lowestPrice = currentPrice;
                    bestGame = new GamePriceDto
                    {
                        Title = storeData.Nome,
                        Price = currentPrice == 0m ? "Grátis" : (storeData.PrecoAtual ?? "Indisponível"),
                        Url = storeData.Link ?? "",
                        Store = storeName
                    };
                }
            }

            if (bestGame != null)
            {
                // Cachear resultado — tempo configurável via appsettings
                var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                await _cache.SetAsync(cacheKey, bestGame, TimeSpan.FromMinutes(expirationMinutes));
                _logger.LogInformation("Resultado cacheado por {Minutes} minutos para: {GameName} na loja {Store}", expirationMinutes, gameName, bestGame.Store);
            }

            return bestGame;
        }

        private decimal ParsePrice(string? priceStr)
        {
            if (string.IsNullOrWhiteSpace(priceStr)) return decimal.MaxValue;
            var lower = priceStr.ToLowerInvariant();
            if (lower.Contains("grátis") || lower.Contains("free") || lower.Contains("gratuito"))
                return 0m;

            var numericPart = priceStr.Replace("R$", "").Replace("BRL", "").Trim();
            if (decimal.TryParse(numericPart, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out decimal result))
            {
                return result;
            }
            return decimal.MaxValue;
        }

        public async Task<List<GameDealDto>> GetTopDealsAsync()
        {
            var cacheKey = "top_deals_grid";

            var cached = await _cache.GetAsync<List<GameDealDto>>(cacheKey);
            if (cached is not null)
            {
                _logger.LogInformation("Cache HIT para o grid de deals");
                return cached;
            }

            _logger.LogInformation("Cache MISS para o grid de deals. Consultando scraper...");

            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var dealsUrl = $"{baseUrl.TrimEnd('/')}/deals";

            var results = await _http.GetFromJsonAsync<List<GameDealDto>>(dealsUrl);

            if (results != null && results.Count > 0)
            {
                var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(expirationMinutes));
                return results;
            }

            return new List<GameDealDto>();
        }
    }
}
