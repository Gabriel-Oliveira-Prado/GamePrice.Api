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
    }
}
