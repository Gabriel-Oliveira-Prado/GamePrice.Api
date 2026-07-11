using GamePrice.Api.Domain.DTOs;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IScraperService
    {
        Task<GamePriceDto?> GetGamePriceAsync(string gameName);
        Task<List<PythonStoreResultDto>?> GetGamePricesAsync(string gameName);
        Task<List<GameDealDto>> GetTopDealsAsync();
        Task<List<GameDealDto>> GetFreeGamesAsync();
    }
}
