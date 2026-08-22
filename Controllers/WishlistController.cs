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
    [Route("api/wishlist")]
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistRepository _wishlist;
        private readonly IUserRepository _users;

        public WishlistController(IWishlistRepository wishlist, IUserRepository users)
        {
            _wishlist = wishlist;
            _users = users;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            return Ok(await _wishlist.GetAsync(user.Id, cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> Add(
            [FromBody] WishlistRequestDto request,
            CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            var item = await _wishlist.AddAsync(
                user.Id,
                request.GameName,
                request.TargetPrice,
                cancellationToken);
            return item is null ? BadRequest() : Ok(item);
        }

        [HttpPut("{wishlistId:guid}")]
        public async Task<IActionResult> Update(
            Guid wishlistId,
            [FromBody] UpdateWishlistRequestDto request,
            CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            var item = await _wishlist.UpdateTargetAsync(
                user.Id,
                wishlistId,
                request.TargetPrice,
                cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpDelete("{wishlistId:guid}")]
        public async Task<IActionResult> Remove(Guid wishlistId, CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            return await _wishlist.RemoveAsync(user.Id, wishlistId, cancellationToken)
                ? NoContent()
                : NotFound();
        }

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
