using GamePrice.Api.Domain.DTOs;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IScraperService
    {
        Task<GamePriceDto?> GetGamePriceAsync(string gameName);
    }
}
