using System;

namespace GamePrice.Api.Domain.Models
{
    public class OfferModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string StoreName { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal OriginalPrice { get; set; }
        public string Discount { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
