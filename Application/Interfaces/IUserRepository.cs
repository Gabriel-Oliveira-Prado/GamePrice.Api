using GamePrice.Api.Domain.Models;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<UserModel?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<bool> RegisterAsync(string name, string email, string password, CancellationToken cancellationToken = default);
        Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<UserModel?> UpdateProfileAsync(
            Guid userId,
            string name,
            string email,
            CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default);
        Task RecordLoginAttemptAsync(
            Guid? userId,
            string email,
            bool succeeded,
            string failureReason,
            string ipAddressHash,
            string userAgent,
            CancellationToken cancellationToken = default);
    }
}
