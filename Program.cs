var builder = WebApplication.CreateBuilder(args);

// Adiciona serviços ao container
builder.Services.AddControllers();
builder.Services.AddHttpClient(); // Necessário para o SearchController chamar o Python

var app = builder.Build();

// Configura o pipeline HTTP
app.UseAuthorization();

app.MapControllers();

// Roda a API na porta 5200 (conforme esperado pelo MVC)
app.Run();