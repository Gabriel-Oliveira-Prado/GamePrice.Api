using GamePrice.Api.Domain.DTOs;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IScraperService
    {
        Task<GamePriceDto?> GetGamePriceAsync(string gameName);
        Task<List<PythonStoreResultDto>?> GetGamePricesAsync(string gameName);
        Task<List<GameSearchSuggestionDto>> SearchGamesAsync(string query, int limit = 8);
        Task<List<GameDealDto>> GetTopDealsAsync(bool forceRefresh = false);
        Task<List<GameDealDto>> GetFreeGamesAsync();
    }
}
