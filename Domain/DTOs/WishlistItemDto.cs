namespace GamePrice.Api.Domain.DTOs
{
    public class WishlistItemDto
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string CurrentPrice { get; set; } = string.Empty;
        public string Store { get; set; } = string.Empty;
        public string StoreLink { get; set; } = string.Empty;
        public int OfferCount { get; set; }
        public decimal? TargetPrice { get; set; }
        public bool TargetReached { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
