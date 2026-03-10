namespace GamePrice.Api.Domain.Models
{
    public class GamePriceModel
    {
        public string Title { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Store { get; set; } = string.Empty;
        public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    }
}
