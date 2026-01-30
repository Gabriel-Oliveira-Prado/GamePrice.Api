var builder = WebApplication.CreateBuilder(args);

// Adiciona serviços ao container
builder.Services.AddControllers();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configura o pipeline HTTP
app.UseAuthorization();

app.MapControllers();

app.Run();