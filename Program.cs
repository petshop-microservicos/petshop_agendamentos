using System.Text.Json.Serialization;
using Agendamentos.Api.Data;
using Agendamentos.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IServicosService, ServicosService>();
builder.Services.AddScoped<IProfissionaisService, ProfissionaisService>();
builder.Services.AddScoped<IAgendamentosService, AgendamentosService>();

// Cliente HTTP do serviço AnimaisTutores (RN04: validar o animal)
builder.Services.AddHttpClient<IAnimaisTutoresClient, AnimaisTutoresClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Servicos:AnimaisTutores:BaseUrl"]
        ?? throw new InvalidOperationException("Servicos:AnimaisTutores:BaseUrl não configurado."));
    c.Timeout = TimeSpan.FromSeconds(5);
});

// Enums como texto no JSON ("Agendado", "Segunda", "EmAndamento")
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var app = builder.Build();

// Cria o banco ao iniciar. Sem migrations no projeto usa EnsureCreated;
// depois de gerar a migration (dotnet ef migrations add Inicial) passa a usar Migrate.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (db.Database.GetMigrations().Any())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sem UseHttpsRedirection: facilita testar por HTTP puro no .http.
// A autenticação (JWT) virá do serviço de usuários; quando entrar:
// app.UseAuthentication(); app.UseAuthorization();

app.MapControllers();

app.Run();