using System.Text;
using GamePrice.Api.Application.Interfaces;
using GamePrice.Api.Application.Services;
using GamePrice.Api.Infrastructure.Repositories;
using GamePrice.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Iniciando GamePrice.Api...");

    var builder = WebApplication.CreateBuilder(args);

    // Usar Serilog como provider de logging
    builder.Host.UseSerilog();

    // === Serviços ===

    // Controllers + JSON
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });

    // HttpClient para o ScraperService
    builder.Services.AddHttpClient();

    // Memory Cache
    builder.Services.AddMemoryCache();

    // Response Caching
    builder.Services.AddResponseCaching();

    // DI — Application Services
    builder.Services.AddScoped<IScraperService, ScraperService>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

    // DI — Infrastructure Repositories
    builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

    // DI — Infrastructure Data
    builder.Services.AddScoped<GamePrice.Api.Infrastructure.DbConnection>();

    // JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("JWT Key não configurada no appsettings.json");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "GamePrice.Api",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "GamePrice",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Ler o token do cookie HttpOnly se não vier no header Authorization
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.ContainsKey("auth_token"))
                {
                    context.Token = context.Request.Cookies["auth_token"];
                }
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "GamePrice API",
            Version = "v1",
            Description = "API para busca de preços de jogos com autenticação JWT"
        });

        // Suporte a JWT no Swagger
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Insira o token JWT: Bearer {token}"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Cookie Policy
    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
        options.Secure = CookieSecurePolicy.SameAsRequest;
        options.MinimumSameSitePolicy = SameSiteMode.Strict;
    });

    var app = builder.Build();

    // === Pipeline HTTP ===

    // Middleware de erro global (primeiro no pipeline)
    app.UseMiddleware<ErrorHandlerMiddleware>();

    // Swagger (apenas em desenvolvimento)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "GamePrice API v1");
            c.RoutePrefix = "swagger";
        });
    }

    // Serilog request logging
    app.UseSerilogRequestLogging();

    // Cookie Policy
    app.UseCookiePolicy();

    // Response Caching
    app.UseResponseCaching();

    // Auth
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("GamePrice.Api iniciada com sucesso na porta configurada");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GamePrice.Api falhou ao iniciar");
}
finally
{
    Log.CloseAndFlush();
}