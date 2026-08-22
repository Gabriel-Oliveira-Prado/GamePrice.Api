using System;

namespace GamePrice.Api.Domain.Models
{
    public class WishlistAlertModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public UserModel User { get; set; } = null!;
        public Guid GameId { get; set; }
        public GameModel Game { get; set; } = null!;
        public long TargetPriceMinor { get; set; }
        public string Currency { get; set; } = "BRL";
        public bool IsTriggered { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
