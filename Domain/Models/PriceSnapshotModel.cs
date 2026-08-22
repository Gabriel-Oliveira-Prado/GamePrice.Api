namespace GamePrice.Api.Domain.Models
{
    public class PriceSnapshotModel
    {
        public long Id { get; set; }
        public Guid OfferId { get; set; }
        public OfferModel Offer { get; set; } = null!;
        public long PriceMinor { get; set; }
        public string Currency { get; set; } = "BRL";
        public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    }
}
