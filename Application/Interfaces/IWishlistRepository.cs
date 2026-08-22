using GamePrice.Api.Domain.DTOs;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IWishlistRepository
    {
        Task<List<WishlistItemDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<WishlistItemDto?> AddAsync(
            Guid userId,
            string gameName,
            decimal? targetPrice,
            CancellationToken cancellationToken = default);
        Task<WishlistItemDto?> UpdateTargetAsync(
            Guid userId,
            Guid wishlistId,
            decimal? targetPrice,
            CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(Guid userId, Guid wishlistId, CancellationToken cancellationToken = default);
    }
}
