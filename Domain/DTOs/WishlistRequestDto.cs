using System.ComponentModel.DataAnnotations;

namespace GamePrice.Api.Domain.DTOs
{
    public class WishlistRequestDto
    {
        [Required]
        [StringLength(300, MinimumLength = 1)]
        public string GameName { get; set; } = string.Empty;

        [Range(0.01, 99999)]
        public decimal? TargetPrice { get; set; }
    }
}
