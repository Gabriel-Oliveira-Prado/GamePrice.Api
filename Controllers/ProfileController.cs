using System.Security.Claims;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.DTOs;
using GamePrice.Api.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamePrice.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly IWishlistRepository _wishlist;
        private readonly ITokenService _tokens;
        private readonly IConfiguration _configuration;

        public ProfileController(
            IUserRepository users,
            IWishlistRepository wishlist,
            ITokenService tokens,
            IConfiguration configuration)
        {
            _users = users;
            _wishlist = wishlist;
            _tokens = tokens;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            return Ok(await ToProfileAsync(user, cancellationToken));
        }

        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] UpdateProfileRequestDto request,
            CancellationToken cancellationToken)
        {
            var currentUser = await GetCurrentUserAsync(cancellationToken);
            if (currentUser is null)
                return Unauthorized();

            var updatedUser = await _users.UpdateProfileAsync(
                currentUser.Id,
                request.Name,
                request.Email,
                cancellationToken);
            if (updatedUser is null)
                return Conflict(new { error = "Este email já está em uso" });

            var expirationMinutes = _configuration.GetValue<int>("Jwt:ExpirationMinutes", 60);
            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
            var token = _tokens.GenerateToken(updatedUser.Id, updatedUser.Email, updatedUser.Name);

            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAt,
                Path = "/"
            });

            return Ok(new ProfileUpdateResponseDto
            {
                Profile = await ToProfileAsync(updatedUser, cancellationToken),
                Token = token,
                ExpiresAt = expiresAt
            });
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            var changed = await _users.ChangePasswordAsync(
                user.Id,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken);
            if (!changed)
                return BadRequest(new { error = "A senha atual está incorreta" });

            return Ok(new { message = "Senha atualizada com sucesso" });
        }

        private async Task<UserProfileDto> ToProfileAsync(
            UserModel user,
            CancellationToken cancellationToken) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            WishlistCount = await _wishlist.CountAsync(user.Id, cancellationToken)
        };

        private async Task<UserModel?> GetCurrentUserAsync(CancellationToken cancellationToken)
        {
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(idValue, out var userId))
            {
                var byId = await _users.GetByIdAsync(userId, cancellationToken);
                if (byId is not null)
                    return byId;
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            return string.IsNullOrWhiteSpace(email)
                ? null
                : await _users.GetByEmailAsync(email, cancellationToken);
        }
    }
}
