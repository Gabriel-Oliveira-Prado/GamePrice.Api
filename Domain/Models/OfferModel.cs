using System;

namespace GamePrice.Api.Domain.Models
{
    public class OfferModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GameId { get; set; }
        public GameModel Game { get; set; } = null!;
        public Guid StoreId { get; set; }
        public StoreModel Store { get; set; } = null!;
        public long CurrentPriceMinor { get; set; }
        public long? OriginalPriceMinor { get; set; }
        public string Currency { get; set; } = "BRL";
        public int? DiscountPercent { get; set; }
        public string RedirectUrl { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public List<PriceSnapshotModel> PriceHistory { get; set; } = new();
    }
}
