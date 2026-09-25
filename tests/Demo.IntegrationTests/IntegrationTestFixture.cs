using Microsoft.Extensions.DependencyInjection;
using Demo.Application;
using Demo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Demo.IntegrationTests;

/// <summary>
/// Fixture compartilhada por TODA a suíte de integração (collection fixture).
/// O Testcontainers sobe UM PostgreSQL real (postgres:17) que fica vivo durante
/// a execução: PostgreSqlContainer → IntegrationTestFixture (IAsyncLifetime)
/// → CustomWebApplicationFactory → WebApplicationFactory&lt;Program&gt; → HttpClient.
/// </summary>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    /// <summary>Cliente HTTP apontando para a aplicação real (com gateway simulado).</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Connection string dinâmica do container (usada por factories extras).</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new CustomWebApplicationFactory(ConnectionString);

        // Migrações aplicadas no banco real do container.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        Client = Factory.CreateClient();
    }

    /// <summary>
    /// Abre um SCOPE de DI novo. Cada teste cria o próprio scope + AppDbContext,
    /// que é DESCARTADO ao final (usando 'await using'). Isso evita que o
    /// ChangeTracker vaze entidades de um Assert para outro, garantindo querys
    /// realmente vindas do PostgreSQL.
    /// </summary>
    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();

    /// <summary>
    /// Limpa o banco entre os testes (independência). TRUNCATE é agressivo e
    /// determinístico: apaga tudo e reinicia a sequência de identidades.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "Customers", "Products", "Orders", "OrderItems" RESTART IDENTITY CASCADE;""");
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync().AsTask();
        await _container.DisposeAsync();
    }
}