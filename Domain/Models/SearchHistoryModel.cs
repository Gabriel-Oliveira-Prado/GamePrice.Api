namespace GamePrice.Api.Domain.Models
{
    public class SearchHistoryModel
    {
        public long Id { get; set; }
        public Guid? UserId { get; set; }
        public UserModel? User { get; set; }
        public string Query { get; set; } = string.Empty;
        public int ResultCount { get; set; }
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    }
}
