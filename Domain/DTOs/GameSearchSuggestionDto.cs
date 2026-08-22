using System.Text.Json.Serialization;

namespace GamePrice.Api.Domain.DTOs
{
    public class GameSearchSuggestionDto
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;

        [JsonPropertyName("store")]
        public string Store { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("isFree")]
        public bool IsFree { get; set; }

        [JsonPropertyName("offerCount")]
        public int OfferCount { get; set; }

        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;
    }
}
