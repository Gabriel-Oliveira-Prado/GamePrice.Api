namespace GamePrice.Api.Domain.Models
{
    public class StoreModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<OfferModel> Offers { get; set; } = new();
    }
}
