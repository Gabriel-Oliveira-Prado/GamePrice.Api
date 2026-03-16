using System.Text.Json.Serialization;

namespace GamePrice.Api.Domain.DTOs
{
    public class GamePriceDto
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("store")]
        public string Store { get; set; } = string.Empty;
    }
}
