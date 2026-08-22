namespace GamePrice.Api.Domain.Models
{
    public class UserModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public List<WishlistAlertModel> WishlistAlerts { get; set; } = new();
        public List<LoginAuditModel> LoginAudits { get; set; } = new();
        public List<SearchHistoryModel> Searches { get; set; } = new();
    }
}
