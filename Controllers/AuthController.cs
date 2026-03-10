using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;

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
        public IActionResult Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = _userRepository.Authenticate(request.Email, request.Password);

            if (user is null)
                return Unauthorized(new { error = "Email ou senha inválidos" });

            var token = _tokenService.GenerateToken(user.Email, user.Name);
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
                ExpiresAt = expiresAt
            });
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (_userRepository.EmailExists(request.Email))
                return Conflict(new { error = "Este email já está cadastrado" });

            var success = _userRepository.Register(request.Name, request.Email, request.Password);

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
    }
}
