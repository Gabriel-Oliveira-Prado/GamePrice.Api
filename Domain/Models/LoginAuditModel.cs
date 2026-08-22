namespace GamePrice.Api.Domain.Models
{
    public class LoginAuditModel
    {
        public long Id { get; set; }
        public Guid? UserId { get; set; }
        public UserModel? User { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public string IpAddressHash { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
