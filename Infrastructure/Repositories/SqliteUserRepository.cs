using System.Security.Cryptography;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.Models;
using GamePrice.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamePrice.Api.Infrastructure.Repositories
{
    public class SqliteUserRepository : IUserRepository
    {
        private const int PasswordIterations = 600_000;
        private readonly GamePriceDbContext _database;
        private readonly ILogger<SqliteUserRepository> _logger;

        public SqliteUserRepository(GamePriceDbContext database, ILogger<SqliteUserRepository> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task<UserModel?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            var user = await _database.Users.SingleOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail && item.IsActive,
                cancellationToken);

            if (user is null || !VerifyPassword(password, user.PasswordHash))
                return null;

            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = user.LastLoginAt.Value;
            await _database.SaveChangesAsync(cancellationToken);
            return user;
        }

        public async Task<bool> RegisterAsync(
            string name,
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (await _database.Users.AnyAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken))
                return false;

            var now = DateTime.UtcNow;
            _database.Users.Add(new UserModel
            {
                Name = name.Trim(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = HashPassword(password),
                CreatedAt = now,
                UpdatedAt = now
            });

            try
            {
                await _database.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException error)
            {
                _logger.LogWarning(error, "Falha ao registrar email duplicado ou invalido");
                return false;
            }
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            return _database.Users
                .AsNoTracking()
                .AnyAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        }

        public Task<UserModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            _database.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);

        public Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            return _database.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.NormalizedEmail == normalizedEmail && item.IsActive,
                    cancellationToken);
        }

        public async Task<UserModel?> UpdateProfileAsync(
            Guid userId,
            string name,
            string email,
            CancellationToken cancellationToken = default)
        {
            var user = await _database.Users.SingleOrDefaultAsync(
                item => item.Id == userId && item.IsActive,
                cancellationToken);
            if (user is null)
                return null;

            var normalizedEmail = NormalizeEmail(email);
            var emailInUse = await _database.Users.AnyAsync(
                item => item.Id != userId && item.NormalizedEmail == normalizedEmail,
                cancellationToken);
            if (emailInUse)
                return null;

            user.Name = name.Trim();
            user.Email = email.Trim();
            user.NormalizedEmail = normalizedEmail;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _database.SaveChangesAsync(cancellationToken);
                return user;
            }
            catch (DbUpdateException error)
            {
                _logger.LogWarning(error, "Falha ao atualizar perfil do usuario {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            var user = await _database.Users.SingleOrDefaultAsync(
                item => item.Id == userId && item.IsActive,
                cancellationToken);
            if (user is null || !VerifyPassword(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _database.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task RecordLoginAttemptAsync(
            Guid? userId,
            string email,
            bool succeeded,
            string failureReason,
            string ipAddressHash,
            string userAgent,
            CancellationToken cancellationToken = default)
        {
            _database.LoginAudits.Add(new LoginAuditModel
            {
                UserId = userId,
                Email = email.Trim().ToLowerInvariant(),
                Succeeded = succeeded,
                FailureReason = succeeded ? string.Empty : Truncate(failureReason, 80),
                IpAddressHash = Truncate(ipAddressHash, 64),
                UserAgent = Truncate(userAgent, 512),
                OccurredAt = DateTime.UtcNow
            });
            await _database.SaveChangesAsync(cancellationToken);
        }

        private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                PasswordIterations,
                HashAlgorithmName.SHA256,
                32);
            return $"PBKDF2-SHA256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" || !int.TryParse(parts[1], out var iterations))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);
                return CryptographicOperations.FixedTimeEquals(expectedHash, computedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}
