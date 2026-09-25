using Demo.Api.Endpoints;
using Demo.Application;
using Demo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Camada de infra + DI. A connection string vem da configuração e pode ser
// sobrescrita pelo WebApplicationFactory nos testes (com a string do container).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
                      ?? "Host=localhost;Port=5432;Database=demo;Username=postgres;Password=postgres;"));

// Expõe o DbContext pela interface da Application (mesma instância scoped).
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Dependência EXTERNA mockável.
builder.Services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();

builder.Services.AddScoped<OrderService>();

// Página de documentação para demo ao vivo.
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(); // Scalar UI em /scalar/v1

app.MapCustomerEndpoints();
app.MapOrderEndpoints();
app.MapProductEndpoints();

app.Run();

// Necessário para que o WebApplicationFactory<Program> encontre a classe correta.
public partial class Program;