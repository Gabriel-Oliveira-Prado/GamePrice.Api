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
        private readonly IGameCatalogRepository _catalog;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScraperService> _logger;

        public ScraperService(
            HttpClient http,
            ICacheService cache,
            IGameCatalogRepository catalog,
            IConfiguration configuration,
            ILogger<ScraperService> logger)
        {
            _http = http;
            _cache = cache;
            _catalog = catalog;
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
                    if (string.IsNullOrEmpty(storeData.Nome) || !HasComparablePrice(storeData))
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
                    var comparableResults = results.Where(HasComparablePrice).ToList();
                    await PersistSearchResultsAsync(gameName, comparableResults);
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

            var numericPart = System.Text.RegularExpressions.Regex.Replace(
                priceStr,
                @"[^0-9,.-]",
                string.Empty);
            if (decimal.TryParse(numericPart, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out decimal result))
            {
                return result;
            }
            return decimal.MaxValue;
        }

        private bool HasComparablePrice(PythonStoreResultDto result)
        {
            var currentPrice = ParsePrice(result.PrecoAtual);
            return currentPrice != decimal.MaxValue;
        }

        public async Task<List<PythonStoreResultDto>?> GetGamePricesAsync(string gameName)
        {
            var cacheKey = $"game_prices_{gameName.ToLowerInvariant().Trim()}";

            var cached = await _cache.GetAsync<List<PythonStoreResultDto>>(cacheKey);
            if (cached is not null)
            {
                _logger.LogInformation("Cache HIT para a lista de preços do jogo: {GameName}", gameName);
                await PersistSearchResultsAsync(gameName, cached);
                return cached;
            }

            var persistedOffers = await _catalog.GetOffersByTitleAsync(gameName);
            if (persistedOffers.Any(result => ParsePrice(result.PrecoAtual) == 0m))
            {
                _logger.LogInformation("Jogo gratuito encontrado no catalogo local: {GameName}", gameName);
                var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                await _cache.SetAsync(cacheKey, persistedOffers, TimeSpan.FromMinutes(expirationMinutes));
                return persistedOffers;
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

                // Mantem apenas precos de compra verificaveis e ordena pelo menor valor.
                results = results
                    .Where(HasComparablePrice)
                    .OrderBy(r => ParsePrice(r.PrecoAtual))
                    .ToList();

                if (results.Count == 0)
                {
                    _logger.LogWarning("Nenhum preco de compra comparavel para: {GameName}", gameName);
                    return null;
                }

                var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(expirationMinutes));
                await PersistSearchResultsAsync(gameName, results);

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

        public async Task<List<GameSearchSuggestionDto>> SearchGamesAsync(string query, int limit = 8)
        {
            var safeLimit = Math.Clamp(limit, 1, 12);
            var normalizedQuery = NormalizeSearchValue(query);
            if (normalizedQuery.Length < 2)
                return new List<GameSearchSuggestionDto>();

            var cacheKey = $"game_search_{normalizedQuery}_{safeLimit}";
            var cached = await _cache.GetAsync<List<GameSearchSuggestionDto>>(cacheKey);
            if (cached is not null)
                return cached;

            var localResults = await _catalog.SearchGamesAsync(query, safeLimit);
            var remoteResults = new List<GameSearchSuggestionDto>();
            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var searchUrl = $"{baseUrl.TrimEnd('/')}/search?query={Uri.EscapeDataString(query)}&limit={safeLimit}";

            try
            {
                remoteResults = await _http.GetFromJsonAsync<List<GameSearchSuggestionDto>>(searchUrl)
                    ?? new List<GameSearchSuggestionDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pesquisa remota de catalogo indisponivel para: {Query}", query);
            }

            var discoveredOffers = remoteResults
                .Where(result => !string.IsNullOrWhiteSpace(result.Title)
                    && !string.IsNullOrWhiteSpace(result.Price)
                    && !string.IsNullOrWhiteSpace(result.Link))
                .Select((result, index) => new GameDealDto
                {
                    Id = index + 1,
                    Title = result.Title,
                    Price = result.IsFree ? "Grátis" : result.Price,
                    Store = result.Store,
                    Platform = MapStoreToPlatform(result.Store),
                    Image = result.Image,
                    Link = result.Link
                })
                .ToList();
            if (discoveredOffers.Count > 0)
                await PersistDealsAsync(discoveredOffers, "catalog-search");

            var merged = new Dictionary<string, GameSearchSuggestionDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in localResults.Concat(remoteResults))
            {
                var key = NormalizeSearchValue(result.Title);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!merged.TryGetValue(key, out var existing))
                {
                    merged[key] = result;
                    continue;
                }

                existing.IsFree |= result.IsFree;
                existing.OfferCount = Math.Max(existing.OfferCount, result.OfferCount);
                if (string.IsNullOrWhiteSpace(existing.Price) || result.IsFree)
                    existing.Price = result.Price;
                if (string.IsNullOrWhiteSpace(existing.Store) || result.IsFree)
                    existing.Store = result.Store;
                if (string.IsNullOrWhiteSpace(existing.Image))
                    existing.Image = result.Image;
                if (string.IsNullOrWhiteSpace(existing.Link))
                    existing.Link = result.Link;
            }

            var results = merged.Values
                .OrderBy(result => NormalizeSearchValue(result.Title).StartsWith(normalizedQuery) ? 0 : 1)
                .ThenByDescending(result => result.OfferCount)
                .ThenBy(result => result.IsFree ? 0 : 1)
                .ThenBy(result => result.Title)
                .Take(safeLimit)
                .ToList();

            await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(5));
            return results;
        }

        public async Task<List<GameDealDto>> GetTopDealsAsync(bool forceRefresh = false)
        {
            var cacheKey = "top_deals_grid";

            var cached = await _cache.GetAsync<List<GameDealDto>>(cacheKey);
            if (cached is not null && !forceRefresh)
            {
                _logger.LogInformation("Cache HIT para o grid de deals");
                return cached;
            }

            _logger.LogInformation(
                forceRefresh
                    ? "Atualizando o grid de deals em segundo plano..."
                    : "Cache MISS para o grid de deals. Buscando preços reais dos jogos em alta...");

            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var dealsUrl = $"{baseUrl.TrimEnd('/')}/deals";

            try
            {
                var directDeals = await _http.GetFromJsonAsync<List<GameDealDto>>(dealsUrl);
                if (directDeals is { Count: > 0 })
                {
                    await PersistDealsAsync(directDeals, "steam-featured");
                    var directDealsExpiration = _configuration.GetValue<int>("Cache:DealsExpirationMinutes", 60);
                    await _cache.SetAsync(cacheKey, directDeals, TimeSpan.FromMinutes(directDealsExpiration));
                    return directDeals;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Endpoint leve de deals indisponível. Consultando o fallback local.");
            }

            if (cached is { Count: > 0 })
            {
                _logger.LogWarning("A atualizacao de ofertas falhou. Mantendo o ultimo grid valido.");
                return cached;
            }

            var persistedDeals = await _catalog.GetLatestDealsAsync();
            if (persistedDeals.Count > 0)
            {
                _logger.LogWarning(
                    "A atualizacao remota falhou. Recuperando {Count} oferta(s) recentes do SQLite.",
                    persistedDeals.Count);
                var persistedExpiration = _configuration.GetValue<int>("Cache:DealsExpirationMinutes", 60);
                await _cache.SetAsync(cacheKey, persistedDeals, TimeSpan.FromMinutes(persistedExpiration));
                return persistedDeals;
            }

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

                            // Sem preco original confiavel, nao fabrica desconto.
                            if (originalPrice == decimal.MaxValue || originalPrice <= currentPrice)
                            {
                                originalPrice = currentPrice;
                            }

                            var discount = "";
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
                                Image = best.Imagem ?? "",
                                Link = best.Link ?? ""
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

            if (dealsList.Count == 0)
            {
                _logger.LogWarning("Nenhuma oferta real disponivel no momento.");
            }

            if (dealsList.Count > 0)
            {
                await PersistDealsAsync(dealsList, "comparison-fallback");
                var expirationMinutes = _configuration.GetValue<int>("Cache:DealsExpirationMinutes", 60);
                await _cache.SetAsync(cacheKey, dealsList, TimeSpan.FromMinutes(expirationMinutes));
                return dealsList;
            }

            return new List<GameDealDto>();
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

        private static string NormalizeSearchValue(string value) =>
            new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

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
                    await PersistDealsAsync(results, "free-games-feed");
                    var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 10);
                    await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(expirationMinutes));
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogos gratuitos no scraper Python");
            }

            return new List<GameDealDto>();
        }

        private async Task PersistDealsAsync(IReadOnlyCollection<GameDealDto> deals, string source)
        {
            try
            {
                await _catalog.SaveDealsAsync(deals, source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao persistir ofertas do feed {Source}", source);
            }
        }

        private async Task PersistSearchResultsAsync(
            string query,
            IReadOnlyCollection<PythonStoreResultDto> results)
        {
            try
            {
                await _catalog.SaveSearchResultsAsync(query, results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao persistir resultados da busca {Query}", query);
            }
        }
    }
}
