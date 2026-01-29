using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace GamePrice.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public SearchController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("A busca não pode estar vazia.");

            // Lógica: API C# chama o Scraper Python (Porta 8000)
            // Aqui é onde futuramente você validará dados ou salvará histórico no banco
            var baseUrl = Environment.GetEnvironmentVariable("SCRAPER_URL") ?? "http://127.0.0.1:8000";
            var scraperUrl = $"{baseUrl}/scraping/search/{query}";
            
            var client = _httpClientFactory.CreateClient();
            
            try 
            {
                var response = await client.GetStringAsync(scraperUrl);
                return Content(response, "application/json");
            }
            catch
            {
                return StatusCode(500, new { error = "Erro de comunicação entre API e Scraper." });
            }
        }
    }
}