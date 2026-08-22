using GamePrice.Api.Domain.DTOs;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IGameCatalogRepository
    {
        Task SaveDealsAsync(
            IReadOnlyCollection<GameDealDto> deals,
            string source,
            CancellationToken cancellationToken = default);

        Task SaveSearchResultsAsync(
            string query,
            IReadOnlyCollection<PythonStoreResultDto> results,
            CancellationToken cancellationToken = default);

        Task<List<GameSearchSuggestionDto>> SearchGamesAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default);

        Task<List<GameDealDto>> GetLatestDealsAsync(
            int limit = 24,
            CancellationToken cancellationToken = default);

        Task<List<PythonStoreResultDto>> GetOffersByTitleAsync(
            string title,
            CancellationToken cancellationToken = default);
    }
}
