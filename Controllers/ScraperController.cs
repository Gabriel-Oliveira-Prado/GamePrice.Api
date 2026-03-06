using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using GamePrice.Api.DTOs;

[ApiController]
[Route("api/[controller]")]
public class ScraperController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public ScraperController(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    [HttpGet("price")]
    public async Task<IActionResult> GetPrice([FromQuery] string gameName)
    {
        if (string.IsNullOrEmpty(gameName))
            return BadRequest("Informe o nome do jogo");
            
        try
        {
            // Chama o Scraper Python lendo a URL da configuração
            var baseUrl = _configuration["ApiSettings:ScraperApiUrl"] ?? "http://localhost:8000";
            var pythonUrl = $"{baseUrl.TrimEnd('/')}/scrape?url={Uri.EscapeDataString(gameName)}";

            var data = await _http.GetFromJsonAsync<GamePriceDto>(pythonUrl);

            if (data == null)
                return NotFound("Jogo não encontrado");

            return Ok(data);
        }
        catch
        {
            return StatusCode(500, "Erro ao buscar jogo no Scraper Python.");
        }
    }
}
