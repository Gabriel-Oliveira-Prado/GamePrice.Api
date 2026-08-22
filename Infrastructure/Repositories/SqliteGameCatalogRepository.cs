using System.Globalization;
using System.Text.RegularExpressions;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.DTOs;
using GamePrice.Api.Domain.Models;
using GamePrice.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Infrastructure.Repositories
{
    public class SqliteGameCatalogRepository : IGameCatalogRepository
    {
        private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
        private readonly GamePriceDbContext _database;

        public SqliteGameCatalogRepository(GamePriceDbContext database)
        {
            _database = database;
        }

        public async Task SaveDealsAsync(
            IReadOnlyCollection<GameDealDto> deals,
            string source,
            CancellationToken cancellationToken = default)
        {
            foreach (var deal in deals.Where(item => !string.IsNullOrWhiteSpace(item.Title)))
            {
                var game = await GetOrCreateGameAsync(deal.Title, deal.Image, cancellationToken);
                var store = await GetOrCreateStoreAsync(deal.Store, cancellationToken);
                await UpsertOfferAsync(
                    game,
                    store,
                    deal.Platform,
                    deal.Price,
                    deal.OldPrice,
                    deal.Discount,
                    deal.Link,
                    deal.Image,
                    source,
                    cancellationToken);
            }

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveSearchResultsAsync(
            string query,
            IReadOnlyCollection<PythonStoreResultDto> results,
            CancellationToken cancellationToken = default)
        {
            foreach (var result in results.Where(item => !string.IsNullOrWhiteSpace(item.Nome)))
            {
                var game = await GetOrCreateGameAsync(result.Nome!, result.Imagem, cancellationToken);
                var store = await GetOrCreateStoreAsync(result.Plataforma, cancellationToken);
                await UpsertOfferAsync(
                    game,
                    store,
                    MapPlatform(result.Plataforma),
                    result.PrecoAtual,
                    result.PrecoOriginal,
                    string.Empty,
                    result.Link,
                    result.Imagem,
                    "comparison",
                    cancellationToken);
            }

            _database.SearchHistory.Add(new SearchHistoryModel
            {
                Query = Truncate(query.Trim(), 300),
                ResultCount = results.Count,
                SearchedAt = DateTime.UtcNow
            });

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<GameSearchSuggestionDto>> SearchGamesAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var normalizedQuery = NormalizeTitle(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
                return new List<GameSearchSuggestionDto>();

            var games = await _database.Games
                .AsNoTracking()
                .Include(game => game.Offers.Where(offer => offer.IsActive))
                .ThenInclude(offer => offer.Store)
                .Where(game => game.NormalizedTitle.Contains(normalizedQuery))
                .OrderBy(game => game.NormalizedTitle.StartsWith(normalizedQuery) ? 0 : 1)
                .ThenBy(game => game.Title)
                .Take(Math.Clamp(limit, 1, 12))
                .ToListAsync(cancellationToken);

            return games.Select(game =>
            {
                var offers = game.Offers
                    .Where(offer => offer.IsActive)
                    .OrderBy(offer => offer.CurrentPriceMinor)
                    .ToList();
                var bestOffer = offers.FirstOrDefault();

                return new GameSearchSuggestionDto
                {
                    Title = game.Title,
                    Price = bestOffer is null ? string.Empty : FormatPrice(bestOffer.CurrentPriceMinor),
                    Store = bestOffer?.Store.Name ?? string.Empty,
                    Image = !string.IsNullOrWhiteSpace(game.ImageUrl)
                        ? game.ImageUrl
                        : bestOffer?.ImageUrl ?? string.Empty,
                    IsFree = bestOffer?.IsFree == true,
                    OfferCount = offers.Select(offer => offer.StoreId).Distinct().Count(),
                    Link = bestOffer?.RedirectUrl ?? string.Empty
                };
            }).ToList();
        }

        public async Task<List<GameDealDto>> GetLatestDealsAsync(
            int limit = 24,
            CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var safeLimit = Math.Clamp(limit, 1, 48);
            var offers = await _database.Offers
                .AsNoTracking()
                .Include(offer => offer.Game)
                .Include(offer => offer.Store)
                .Where(offer => offer.IsActive
                    && offer.ObservedAt >= cutoff
                    && (offer.Source == "steam-featured" || offer.Source == "comparison-fallback"))
                .OrderByDescending(offer => offer.ObservedAt)
                .Take(safeLimit * 4)
                .ToListAsync(cancellationToken);

            if (offers.Count == 0)
                return new List<GameDealDto>();

            var latestBatchCutoff = offers.Max(offer => offer.ObservedAt).AddMinutes(-2);
            return offers
                .Where(offer => offer.ObservedAt >= latestBatchCutoff)
                .GroupBy(offer => offer.GameId)
                .Select(group => group
                    .OrderBy(offer => offer.CurrentPriceMinor)
                    .ThenByDescending(offer => offer.ObservedAt)
                    .First())
                .OrderByDescending(offer => offer.ObservedAt)
                .Take(safeLimit)
                .Select((offer, index) => new GameDealDto
                {
                    Id = index + 1,
                    Title = offer.Game.Title,
                    Price = FormatDealPrice(offer.CurrentPriceMinor),
                    OldPrice = offer.OriginalPriceMinor is > 0
                        && offer.OriginalPriceMinor.Value > offer.CurrentPriceMinor
                        ? FormatDealPrice(offer.OriginalPriceMinor.Value)
                        : string.Empty,
                    Discount = offer.DiscountPercent is > 0
                        ? $"-{offer.DiscountPercent}%"
                        : string.Empty,
                    Platform = offer.Platform,
                    Store = offer.Store.Name,
                    Image = !string.IsNullOrWhiteSpace(offer.ImageUrl)
                        ? offer.ImageUrl
                        : offer.Game.ImageUrl,
                    Link = offer.RedirectUrl
                })
                .ToList();
        }

        public async Task<List<PythonStoreResultDto>> GetOffersByTitleAsync(
            string title,
            CancellationToken cancellationToken = default)
        {
            var normalizedTitle = NormalizeTitle(title);
            var game = await _database.Games
                .AsNoTracking()
                .Include(item => item.Offers.Where(offer => offer.IsActive))
                .ThenInclude(offer => offer.Store)
                .SingleOrDefaultAsync(item => item.NormalizedTitle == normalizedTitle, cancellationToken);

            if (game is null)
                return new List<PythonStoreResultDto>();

            return game.Offers
                .Where(offer => offer.IsActive)
                .OrderBy(offer => offer.CurrentPriceMinor)
                .Select(offer => new PythonStoreResultDto
                {
                    Plataforma = offer.Store.Name,
                    Nome = game.Title,
                    PrecoAtual = FormatPrice(offer.CurrentPriceMinor),
                    PrecoOriginal = offer.OriginalPriceMinor is > 0
                        ? FormatPrice(offer.OriginalPriceMinor.Value)
                        : string.Empty,
                    Imagem = !string.IsNullOrWhiteSpace(offer.ImageUrl) ? offer.ImageUrl : game.ImageUrl,
                    Link = offer.RedirectUrl
                })
                .ToList();
        }

        private async Task<GameModel> GetOrCreateGameAsync(
            string title,
            string? imageUrl,
            CancellationToken cancellationToken)
        {
            var normalizedTitle = NormalizeTitle(title);
            var game = _database.Games.Local.FirstOrDefault(item => item.NormalizedTitle == normalizedTitle)
                ?? await _database.Games.FirstOrDefaultAsync(
                    item => item.NormalizedTitle == normalizedTitle,
                    cancellationToken);

            var now = DateTime.UtcNow;
            if (game is null)
            {
                game = new GameModel
                {
                    Title = title.Trim(),
                    NormalizedTitle = normalizedTitle,
                    ImageUrl = imageUrl?.Trim() ?? string.Empty,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _database.Games.Add(game);
                return game;
            }

            if (IsBetterDisplayTitle(title, game.Title))
                game.Title = title.Trim();
            if (!string.IsNullOrWhiteSpace(imageUrl))
                game.ImageUrl = imageUrl.Trim();
            game.UpdatedAt = now;
            return game;
        }

        private async Task<StoreModel> GetOrCreateStoreAsync(string? storeName, CancellationToken cancellationToken)
        {
            var name = string.IsNullOrWhiteSpace(storeName) ? "Loja oficial" : storeName.Trim();
            var slug = StoreSlug(name);
            var store = _database.Stores.Local.FirstOrDefault(item => item.Slug == slug)
                ?? await _database.Stores.FirstOrDefaultAsync(item => item.Slug == slug, cancellationToken);

            if (store is not null)
                return store;

            store = new StoreModel
            {
                Name = name,
                Slug = slug,
                WebsiteUrl = StoreWebsite(slug),
                CreatedAt = DateTime.UtcNow
            };
            _database.Stores.Add(store);
            return store;
        }

        private async Task UpsertOfferAsync(
            GameModel game,
            StoreModel store,
            string? platform,
            string? currentPrice,
            string? originalPrice,
            string? discount,
            string? redirectUrl,
            string? imageUrl,
            string source,
            CancellationToken cancellationToken)
        {
            var currentPriceMinor = ParsePriceMinor(currentPrice);
            if (currentPriceMinor is null)
                return;

            var normalizedPlatform = MapPlatform(platform);
            var offer = _database.Offers.Local.FirstOrDefault(item =>
                    item.GameId == game.Id
                    && item.StoreId == store.Id
                    && item.Platform == normalizedPlatform)
                ?? await _database.Offers.FirstOrDefaultAsync(item =>
                    item.GameId == game.Id
                    && item.StoreId == store.Id
                    && item.Platform == normalizedPlatform,
                    cancellationToken);

            var now = DateTime.UtcNow;
            var priceChanged = offer is null || offer.CurrentPriceMinor != currentPriceMinor.Value;
            if (offer is null)
            {
                offer = new OfferModel
                {
                    GameId = game.Id,
                    StoreId = store.Id,
                    Platform = normalizedPlatform
                };
                _database.Offers.Add(offer);
            }

            offer.CurrentPriceMinor = currentPriceMinor.Value;
            offer.OriginalPriceMinor = ParsePriceMinor(originalPrice);
            offer.Currency = "BRL";
            offer.DiscountPercent = ParseDiscount(discount);
            offer.RedirectUrl = redirectUrl?.Trim() ?? string.Empty;
            offer.ImageUrl = imageUrl?.Trim() ?? string.Empty;
            offer.Source = Truncate(source, 80);
            offer.IsFree = currentPriceMinor.Value == 0;
            offer.IsActive = true;
            offer.ObservedAt = now;

            if (priceChanged)
            {
                _database.PriceSnapshots.Add(new PriceSnapshotModel
                {
                    OfferId = offer.Id,
                    PriceMinor = currentPriceMinor.Value,
                    Currency = "BRL",
                    ObservedAt = now
                });
            }
        }

        private static long? ParsePriceMinor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var lower = value.ToLowerInvariant();
            if (lower.Contains("grátis") || lower.Contains("gratis") || lower.Contains("gratuito") || lower == "free")
                return 0;

            var numeric = Regex.Replace(value, @"[^0-9,.-]", string.Empty);
            if (string.IsNullOrWhiteSpace(numeric))
                return null;

            if (!decimal.TryParse(numeric, NumberStyles.Number, BrazilianCulture, out var parsed)
                && !decimal.TryParse(numeric, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                return null;

            return checked((long)decimal.Round(parsed * 100, 0, MidpointRounding.AwayFromZero));
        }

        private static int? ParseDiscount(string? value)
        {
            var match = Regex.Match(value ?? string.Empty, @"\d+");
            return match.Success && int.TryParse(match.Value, out var discount) ? discount : null;
        }

        private static string NormalizeTitle(string value) =>
            Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", " ");

        private static bool IsBetterDisplayTitle(string candidate, string current) =>
            !string.IsNullOrWhiteSpace(candidate)
            && candidate.Any(char.IsLower)
            && !current.Any(char.IsLower);

        private static string FormatPrice(long priceMinor) => priceMinor == 0
            ? "Grátis"
            : $"R$ {(priceMinor / 100m).ToString("N2", BrazilianCulture)}";

        private static string FormatDealPrice(long priceMinor) => priceMinor == 0
            ? "Grátis"
            : (priceMinor / 100m).ToString("N2", BrazilianCulture);

        private static string MapPlatform(string? value)
        {
            var platform = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (platform.Contains("playstation") || platform.Contains("ps store")) return "playstation";
            if (platform.Contains("xbox")) return "xbox";
            if (platform.Contains("nintendo")) return "nintendo";
            return "pc";
        }

        private static string StoreSlug(string value)
        {
            var store = value.ToLowerInvariant();
            if (store.Contains("steam")) return "steam";
            if (store.Contains("epic")) return "epic-games";
            if (store.Contains("playstation") || store.Contains("ps store")) return "playstation";
            if (store.Contains("xbox")) return "xbox";
            if (store.Contains("nintendo")) return "nintendo";
            if (store.Contains("gog")) return "gog";
            if (store.Contains("nuuvem")) return "nuuvem";
            if (store.Contains("itch")) return "itch-io";
            return Regex.Replace(store, @"[^a-z0-9]+", "-").Trim('-');
        }

        private static string StoreWebsite(string slug) => slug switch
        {
            "steam" => "https://store.steampowered.com",
            "epic-games" => "https://store.epicgames.com",
            "playstation" => "https://store.playstation.com",
            "xbox" => "https://www.xbox.com",
            "nintendo" => "https://www.nintendo.com",
            "gog" => "https://www.gog.com",
            "nuuvem" => "https://www.nuuvem.com",
            "itch-io" => "https://itch.io",
            _ => string.Empty
        };

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}
