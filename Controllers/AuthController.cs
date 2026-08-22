using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace GamePrice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserRepository userRepository,
            ITokenService tokenService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userRepository.AuthenticateAsync(request.Email, request.Password, cancellationToken);

            if (user is null)
            {
                await _userRepository.RecordLoginAttemptAsync(
                    null,
                    request.Email,
                    false,
                    "invalid_credentials",
                    GetIpAddressHash(),
                    Request.Headers.UserAgent.ToString(),
                    cancellationToken);
                return Unauthorized(new { error = "Email ou senha inválidos" });
            }

            await _userRepository.RecordLoginAttemptAsync(
                user.Id,
                user.Email,
                true,
                string.Empty,
                GetIpAddressHash(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            var token = _tokenService.GenerateToken(user.Id, user.Email, user.Name);
            var expirationMinutes = _configuration.GetValue<int>("Jwt:ExpirationMinutes", 60);
            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            // Seta cookie HttpOnly com o token JWT
            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAt,
                Path = "/"
            });

            _logger.LogInformation("Login bem-sucedido para: {Email}", user.Email);

            return Ok(new TokenResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                Name = user.Name,
                Email = user.Email
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
                return Conflict(new { error = "Este email já está cadastrado" });

            var success = await _userRepository.RegisterAsync(
                request.Name,
                request.Email,
                request.Password,
                cancellationToken);

            if (!success)
                return StatusCode(500, new { error = "Erro ao registrar usuário" });

            _logger.LogInformation("Novo usuário registrado: {Email}", request.Email);

            return Created("", new { message = "Usuário cadastrado com sucesso" });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // Limpa o cookie de autenticação
            Response.Cookies.Delete("auth_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            _logger.LogInformation("Logout realizado");

            return Ok(new { message = "Logout realizado com sucesso" });
        }

        private string GetIpAddressHash()
        {
            var address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var salt = _configuration["Security:AuditSalt"]
                ?? _configuration["Jwt:Key"]
                ?? "GamePrice";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{address}")));
        }
    }
}
