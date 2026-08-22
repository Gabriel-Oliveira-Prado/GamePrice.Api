using System.ComponentModel.DataAnnotations;

namespace GamePrice.Api.Domain.DTOs
{
    public class UpdateWishlistRequestDto
    {
        [Range(0.01, 99999)]
        public decimal? TargetPrice { get; set; }
    }
}
