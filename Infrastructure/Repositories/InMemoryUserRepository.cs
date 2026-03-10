using System.Security.Cryptography;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GamePrice.Api.Infrastructure.Repositories
{
    public class InMemoryUserRepository : IUserRepository
    {
        private static readonly List<UserModel> _users = new();
        private static readonly object _lock = new();
        private readonly ILogger<InMemoryUserRepository> _logger;

        public InMemoryUserRepository(ILogger<InMemoryUserRepository> logger)
        {
            _logger = logger;

            // Seed: adiciona um usuário admin se não existir
            lock (_lock)
            {
                if (!_users.Any(u => u.Email == "admin@gameprice.com"))
                {
                    _users.Add(new UserModel
                    {
                        Name = "Admin",
                        Email = "admin@gameprice.com",
                        PasswordHash = HashPassword("Admin123!")
                    });
                    _logger.LogInformation("Usuário admin seed criado: admin@gameprice.com");
                }
            }
        }

        public UserModel? Authenticate(string email, string password)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u =>
                    u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

                if (user is null)
                {
                    _logger.LogWarning("Tentativa de login com email não encontrado: {Email}", email);
                    return null;
                }

                if (!VerifyPassword(password, user.PasswordHash))
                {
                    _logger.LogWarning("Senha incorreta para o email: {Email}", email);
                    return null;
                }

                _logger.LogInformation("Login bem-sucedido para: {Email}", email);
                return user;
            }
        }

        public bool Register(string name, string email, string password)
        {
            lock (_lock)
            {
                if (EmailExists(email))
                {
                    _logger.LogWarning("Tentativa de registro com email já existente: {Email}", email);
                    return false;
                }

                var user = new UserModel
                {
                    Name = name,
                    Email = email,
                    PasswordHash = HashPassword(password)
                };

                _users.Add(user);
                _logger.LogInformation("Novo usuário registrado: {Email}", email);
                return true;
            }
        }

        public bool EmailExists(string email)
        {
            return _users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

            return CryptographicOperations.FixedTimeEquals(hash, computedHash);
        }
    }
}
