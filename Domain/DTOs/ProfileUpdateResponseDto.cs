namespace GamePrice.Api.Domain.DTOs
{
    public class ProfileUpdateResponseDto
    {
        public UserProfileDto Profile { get; set; } = new();
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
