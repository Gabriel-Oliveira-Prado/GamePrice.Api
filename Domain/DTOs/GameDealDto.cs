using System.Text.Json.Serialization;

namespace GamePrice.Api.Domain.DTOs
{
    public class GameDealDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;

        [JsonPropertyName("oldPrice")]
        public string OldPrice { get; set; } = string.Empty;

        [JsonPropertyName("discount")]
        public string Discount { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("store")]
        public string Store { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;
    }
}
