using System.Net.Http.Json;
using System.Linq;
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

            try
            {
                // O scraper Python retorna uma LISTA de resultados
                var results = await _http.GetFromJsonAsync<List<PythonStoreResultDto>>(pythonUrl);

                if (results == null || results.Count == 0)
                {
                    _logger.LogWarning("Nenhum resultado retornado pelo scraper para: {GameName}", gameName);
                    return null;
                }

                _logger.LogInformation("Scraper retornou {Count} resultado(s) para: {GameName}", results.Count, gameName);

                GamePriceDto? bestGame = null;
                decimal lowestPrice = decimal.MaxValue;

                foreach (var storeData in results)
                {
                    if (string.IsNullOrEmpty(storeData.Nome))
                        continue;

                    decimal currentPrice = ParsePrice(storeData.PrecoAtual);

                    _logger.LogInformation("Loja: {Store} | Preço: {Price} (parsed: {Parsed})",
                        storeData.Plataforma ?? "Desconhecida",
                        storeData.PrecoAtual ?? "N/A",
                        currentPrice);

                    if (currentPrice < lowestPrice)
                    {
                        lowestPrice = currentPrice;
                        bestGame = new GamePriceDto
                        {
                            Title = storeData.Nome,
                            Price = currentPrice == 0m ? "Grátis" : (storeData.PrecoAtual ?? "Indisponível"),
                            Url = storeData.Link ?? "",
                            Store = storeData.Plataforma ?? "Desconhecida",
                            Image = storeData.Imagem ?? ""
                        };
                    }
                }

                if (bestGame != null)
                {
                    var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                    await _cache.SetAsync(cacheKey, bestGame, TimeSpan.FromMinutes(expirationMinutes));
                    _logger.LogInformation("Melhor preço: {Store} por {Price} — cacheado por {Minutes}min para: {GameName}",
                        bestGame.Store, bestGame.Price, expirationMinutes, gameName);
                }

                return bestGame;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de conexão com o scraper Python para: {GameName}", gameName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao buscar preço para: {GameName}", gameName);
                return null;
            }
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

        public async Task<List<PythonStoreResultDto>?> GetGamePricesAsync(string gameName)
        {
            var cacheKey = $"game_prices_{gameName.ToLowerInvariant().Trim()}";

            var cached = await _cache.GetAsync<List<PythonStoreResultDto>>(cacheKey);
            if (cached is not null)
            {
                _logger.LogInformation("Cache HIT para a lista de preços do jogo: {GameName}", gameName);
                return cached;
            }

            _logger.LogInformation("Cache MISS para a lista de preços do jogo: {GameName}. Consultando scraper...", gameName);

            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var pythonUrl = $"{baseUrl.TrimEnd('/')}/scrape?url={Uri.EscapeDataString(gameName)}";

            _logger.LogInformation("Chamando scraper Python em: {Url}", pythonUrl);

            try
            {
                var results = await _http.GetFromJsonAsync<List<PythonStoreResultDto>>(pythonUrl);

                if (results == null || results.Count == 0)
                {
                    _logger.LogWarning("Nenhum resultado retornado pelo scraper para: {GameName}", gameName);
                    return null;
                }

                _logger.LogInformation("Scraper retornou {Count} resultado(s) para: {GameName}", results.Count, gameName);

                // Ordenar por menor preço atual
                results = results.OrderBy(r => ParsePrice(r.PrecoAtual)).ToList();

                var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(expirationMinutes));

                return results;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de conexão com o scraper Python para: {GameName}", gameName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao buscar preços para: {GameName}", gameName);
                return null;
            }
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

            _logger.LogInformation("Cache MISS para o grid de deals. Buscando preços reais dos jogos em alta...");

            var trendingGames = new List<string>
            {
                "Elden Ring",
                "Cyberpunk 2077",
                "Grand Theft Auto V",
                "Red Dead Redemption 2",
                "The Witcher 3: Wild Hunt",
                "Hogwarts Legacy",
                "Baldur's Gate 3",
                "Alan Wake 2"
            };

            var dealsList = new List<GameDealDto>();

            try
            {
                // Busca em paralelo
                var tasks = trendingGames.Select(async (game, index) =>
                {
                    try
                    {
                        var prices = await GetGamePricesAsync(game);
                        if (prices != null && prices.Count > 0)
                        {
                            var best = prices.First(); // Já ordenado pelo menor preço
                            decimal currentPrice = ParsePrice(best.PrecoAtual);
                            decimal originalPrice = ParsePrice(best.PrecoOriginal);

                            // Trata preço original igual a zero ou indisponível
                            if (originalPrice == decimal.MaxValue || originalPrice <= currentPrice)
                            {
                                originalPrice = currentPrice * 1.5m; // Simula preço sem desconto se não retornar
                            }

                            var discount = "-0%";
                            if (originalPrice > currentPrice && originalPrice > 0)
                            {
                                var pct = Math.Round((1 - (currentPrice / originalPrice)) * 100);
                                discount = $"-{pct}%";
                            }

                            // Limpa e formata strings
                            var cleanPrice = best.PrecoAtual?.Replace("R$", "").Replace("BRL", "").Trim() ?? "0,00";
                            var cleanOldPrice = originalPrice == currentPrice ? "" : $"{originalPrice:N2}".Replace(".", ",");

                            return new GameDealDto
                            {
                                Id = index + 1,
                                Title = best.Nome ?? game,
                                Price = currentPrice == 0 ? "Grátis" : cleanPrice,
                                OldPrice = cleanOldPrice,
                                Discount = discount,
                                Platform = MapStoreToPlatform(best.Plataforma),
                                Store = best.Plataforma ?? "Desconhecida",
                                Image = best.Imagem ?? ""
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao buscar preço em tempo real para o destaque {Game}", game);
                    }
                    return null;
                });

                var results = await Task.WhenAll(tasks);
                dealsList = results.Where(d => d != null).Select(d => d!).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento paralelo de ofertas em destaque");
            }

            // Fallback caso a busca real falhe por completo ou retorne vazia
            if (dealsList.Count == 0)
            {
                _logger.LogWarning("Retornando fallback estático para ofertas em destaque.");
                dealsList = GetFallbackDeals();
            }

            if (dealsList.Count > 0)
            {
                var expirationMinutes = _configuration.GetValue<int>("Cache:DealsExpirationMinutes", 60);
                await _cache.SetAsync(cacheKey, dealsList, TimeSpan.FromMinutes(expirationMinutes));
            }

            return dealsList;
        }

        private string MapStoreToPlatform(string? store)
        {
            if (string.IsNullOrEmpty(store)) return "pc";
            var s = store.ToLowerInvariant();
            if (s.Contains("playstation") || s.Contains("ps")) return "playstation";
            if (s.Contains("xbox")) return "xbox";
            if (s.Contains("nintendo")) return "nintendo";
            return "pc";
        }

        private List<GameDealDto> GetFallbackDeals()
        {
            return new List<GameDealDto>
            {
                new() { Id = 1, Title = "Elden Ring", Price = "149,90", OldPrice = "229,90", Discount = "-35%", Platform = "pc", Store = "Steam", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1245620/header.jpg" },
                new() { Id = 2, Title = "Cyberpunk 2077", Price = "99,90", OldPrice = "199,90", Discount = "-50%", Platform = "pc", Store = "Steam", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1091500/header.jpg" },
                new() { Id = 3, Title = "Grand Theft Auto V", Price = "37,42", OldPrice = "149,70", Discount = "-75%", Platform = "pc", Store = "Epic Games", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/271590/header.jpg" },
                new() { Id = 4, Title = "Red Dead Redemption 2", Price = "89,90", OldPrice = "299,90", Discount = "-70%", Platform = "pc", Store = "Epic Games", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1174180/header.jpg" }
            };
        }

        public async Task<List<GameDealDto>> GetFreeGamesAsync()
        {
            var cacheKey = "free_games_grid";

            var cached = await _cache.GetAsync<List<GameDealDto>>(cacheKey);
            if (cached is not null)
            {
                _logger.LogInformation("Cache HIT para o grid de jogos grátis");
                return cached;
            }

            _logger.LogInformation("Cache MISS para o grid de jogos grátis. Consultando scraper...");

            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var freeUrl = $"{baseUrl.TrimEnd('/')}/free-games";

            try
            {
                var results = await _http.GetFromJsonAsync<List<GameDealDto>>(freeUrl);

                if (results != null && results.Count > 0)
                {
                    var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                    await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(expirationMinutes));
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogos gratuitos no scraper Python");
            }

            // Fallback caso a API esteja fora
            return new List<GameDealDto>
            {
                new() { Id = 1, Title = "Counter-Strike 2", Price = "Grátis", OldPrice = "Gratuito", Discount = "F2P", Platform = "pc", Store = "Steam", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/730/capsule_616x353.jpg" },
                new() { Id = 2, Title = "Apex Legends", Price = "Grátis", OldPrice = "Gratuito", Discount = "F2P", Platform = "pc", Store = "Steam", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1172470/capsule_616x353.jpg" },
                new() { Id = 3, Title = "PUBG: BATTLEGROUNDS", Price = "Grátis", OldPrice = "Gratuito", Discount = "F2P", Platform = "pc", Store = "Steam", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/578080/capsule_616x353.jpg" },
                new() { Id = 4, Title = "Team Fortress 2", Price = "Grátis", OldPrice = "Gratuito", Discount = "F2P", Platform = "pc", Store = "Steam", Image = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/440/capsule_616x353.jpg" }
            };
        }
    }
}
