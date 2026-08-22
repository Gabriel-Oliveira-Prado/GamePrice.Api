using Microsoft.AspNetCore.Mvc;
using GamePrice.Api.Application.Interfaces;

namespace GamePrice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScraperController : ControllerBase
    {
        private readonly IScraperService _scraperService;
        private readonly ILogger<ScraperController> _logger;

        public ScraperController(IScraperService scraperService, ILogger<ScraperController> logger)
        {
            _scraperService = scraperService;
            _logger = logger;
        }

        [HttpGet("price")]
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "gameName" })]
        public async Task<IActionResult> GetPrice([FromQuery] string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return BadRequest(new { error = "Informe o nome do jogo" });

            _logger.LogInformation("Buscando preço para o jogo: {GameName}", gameName);

            var data = await _scraperService.GetGamePriceAsync(gameName);

            if (data is null)
                return NotFound(new { error = "Jogo não encontrado" });

            return Ok(data);
        }

        [HttpGet("prices")]
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "gameName" })]
        public async Task<IActionResult> GetPrices([FromQuery] string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return BadRequest(new { error = "Informe o nome do jogo" });

            _logger.LogInformation("Buscando todos os preços para o jogo: {GameName}", gameName);

            var data = await _scraperService.GetGamePricesAsync(gameName);

            if (data is null || data.Count == 0)
                return NotFound(new { error = "Jogo não encontrado" });

            return Ok(data);
        }

        [HttpGet("search")]
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "query", "limit" })]
        public async Task<IActionResult> SearchGames([FromQuery] string query, [FromQuery] int limit = 8)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return Ok(Array.Empty<object>());

            var data = await _scraperService.SearchGamesAsync(query.Trim(), Math.Clamp(limit, 1, 12));
            return Ok(data);
        }

        [HttpGet("deals")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetDeals()
        {
            _logger.LogInformation("Requisitando os destaques de ofertas");

            var data = await _scraperService.GetTopDealsAsync();

            if (data == null || data.Count == 0)
                return NotFound(new { error = "Nenhuma oferta encontrada" });

            return Ok(data);
        }

        [HttpGet("free-games")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> GetFreeGames()
        {
            _logger.LogInformation("Requisitando os jogos gratuitos");

            var data = await _scraperService.GetFreeGamesAsync();

            if (data == null || data.Count == 0)
                return NotFound(new { error = "Nenhum jogo gratuito encontrado" });

            return Ok(data);
        }
    }
}
