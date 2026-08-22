using System;
using System.Collections.Generic;

namespace GamePrice.Api.Domain.Models
{
    public class GameModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string NormalizedTitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public List<OfferModel> Offers { get; set; } = new();
        public List<WishlistAlertModel> WishlistAlerts { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
