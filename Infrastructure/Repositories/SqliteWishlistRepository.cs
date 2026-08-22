using System.Globalization;
using System.Text.RegularExpressions;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.DTOs;
using GamePrice.Api.Domain.Models;
using GamePrice.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Infrastructure.Repositories
{
    public class SqliteWishlistRepository : IWishlistRepository
    {
        private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
        private readonly GamePriceDbContext _database;

        public SqliteWishlistRepository(GamePriceDbContext database)
        {
            _database = database;
        }

        public async Task<List<WishlistItemDto>> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var items = await WishlistQuery()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
            return items.Select(MapItem).ToList();
        }

        public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
            _database.WishlistAlerts.CountAsync(item => item.UserId == userId, cancellationToken);

        public async Task<WishlistItemDto?> AddAsync(
            Guid userId,
            string gameName,
            decimal? targetPrice,
            CancellationToken cancellationToken = default)
        {
            var normalizedTitle = NormalizeTitle(gameName);
            var game = await _database.Games.SingleOrDefaultAsync(
                item => item.NormalizedTitle == normalizedTitle,
                cancellationToken);

            if (game is null)
            {
                var now = DateTime.UtcNow;
                game = new GameModel
                {
                    Title = gameName.Trim(),
                    NormalizedTitle = normalizedTitle,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _database.Games.Add(game);
            }

            var item = await _database.WishlistAlerts.SingleOrDefaultAsync(
                entry => entry.UserId == userId && entry.GameId == game.Id,
                cancellationToken);
            var nowUtc = DateTime.UtcNow;
            if (item is null)
            {
                item = new WishlistAlertModel
                {
                    UserId = userId,
                    GameId = game.Id,
                    TargetPriceMinor = ToMinorUnits(targetPrice),
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                _database.WishlistAlerts.Add(item);
            }
            else if (targetPrice.HasValue)
            {
                item.TargetPriceMinor = ToMinorUnits(targetPrice);
                item.UpdatedAt = nowUtc;
            }

            await _database.SaveChangesAsync(cancellationToken);
            return await GetItemAsync(userId, item.Id, cancellationToken);
        }

        public async Task<WishlistItemDto?> UpdateTargetAsync(
            Guid userId,
            Guid wishlistId,
            decimal? targetPrice,
            CancellationToken cancellationToken = default)
        {
            var item = await _database.WishlistAlerts.SingleOrDefaultAsync(
                entry => entry.Id == wishlistId && entry.UserId == userId,
                cancellationToken);
            if (item is null)
                return null;

            item.TargetPriceMinor = ToMinorUnits(targetPrice);
            item.UpdatedAt = DateTime.UtcNow;
            await _database.SaveChangesAsync(cancellationToken);
            return await GetItemAsync(userId, item.Id, cancellationToken);
        }

        public async Task<bool> RemoveAsync(
            Guid userId,
            Guid wishlistId,
            CancellationToken cancellationToken = default)
        {
            var item = await _database.WishlistAlerts.SingleOrDefaultAsync(
                entry => entry.Id == wishlistId && entry.UserId == userId,
                cancellationToken);
            if (item is null)
                return false;

            _database.WishlistAlerts.Remove(item);
            await _database.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task<WishlistItemDto?> GetItemAsync(
            Guid userId,
            Guid wishlistId,
            CancellationToken cancellationToken)
        {
            var item = await WishlistQuery().SingleOrDefaultAsync(
                entry => entry.Id == wishlistId && entry.UserId == userId,
                cancellationToken);
            return item is null ? null : MapItem(item);
        }

        private IQueryable<WishlistAlertModel> WishlistQuery() =>
            _database.WishlistAlerts
                .AsNoTracking()
                .Include(item => item.Game)
                .ThenInclude(game => game.Offers)
                .ThenInclude(offer => offer.Store);

        private static WishlistItemDto MapItem(WishlistAlertModel item)
        {
            var offers = item.Game.Offers
                .Where(offer => offer.IsActive)
                .OrderBy(offer => offer.CurrentPriceMinor)
                .ToList();
            var bestOffer = offers.FirstOrDefault();
            decimal? targetPrice = item.TargetPriceMinor > 0 ? item.TargetPriceMinor / 100m : null;
            var targetReached = item.TargetPriceMinor > 0
                && bestOffer?.CurrentPriceMinor <= item.TargetPriceMinor;

            return new WishlistItemDto
            {
                Id = item.Id,
                GameId = item.GameId,
                Title = item.Game.Title,
                Image = !string.IsNullOrWhiteSpace(bestOffer?.ImageUrl)
                    ? bestOffer.ImageUrl
                    : item.Game.ImageUrl,
                CurrentPrice = bestOffer is null ? "Sem oferta" : FormatPrice(bestOffer.CurrentPriceMinor),
                Store = bestOffer?.Store.Name ?? string.Empty,
                StoreLink = bestOffer?.RedirectUrl ?? string.Empty,
                OfferCount = offers.Select(offer => offer.StoreId).Distinct().Count(),
                TargetPrice = targetPrice,
                TargetReached = targetReached,
                CreatedAt = item.CreatedAt
            };
        }

        private static long ToMinorUnits(decimal? value) => value is > 0
            ? checked((long)decimal.Round(value.Value * 100, 0, MidpointRounding.AwayFromZero))
            : 0;

        private static string NormalizeTitle(string value) =>
            Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", " ");

        private static string FormatPrice(long priceMinor) => priceMinor == 0
            ? "Grátis"
            : $"R$ {(priceMinor / 100m).ToString("N2", BrazilianCulture)}";
    }
}
