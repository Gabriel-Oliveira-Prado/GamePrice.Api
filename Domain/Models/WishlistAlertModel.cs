using System;

namespace GamePrice.Api.Domain.Models
{
    public class WishlistAlertModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserEmail { get; set; } = string.Empty;
        public string GameTitle { get; set; } = string.Empty;
        public decimal TargetPrice { get; set; }
        public bool IsTriggered { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
