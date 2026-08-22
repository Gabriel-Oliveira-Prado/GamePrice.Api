using System.ComponentModel.DataAnnotations;

namespace GamePrice.Api.Domain.DTOs
{
    public class UpdateProfileRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;
    }
}
