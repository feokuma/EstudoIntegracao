using Microsoft.Extensions.DependencyInjection;
using Demo.Application;
using Demo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Demo.IntegrationTests;

/// <summary>
/// Fixture compartilhada por TODA a suíte de integração (collection fixture):
///
///   Testcontainers inicia UM PostgreSQL real (postgres:17) e mantém vivo durante
///   a execução. O caminho de criação é:
///
///   PostgreSqlContainer
///        → integration test fixture (IAsyncLifetime)
///        → CustomWebApplicationFactory
///        → WebApplicationFactory<Program>
///        → HttpClient
///
/// O ciclo de vida assíncrono do xunit.v3 (IAsyncLifetime) garante que o container
/// suba antes do primeiro teste e seja removido ao final.
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
        // 1) Sobe o PostgreSQL real (download da imagem na primeira vez).
        await _container.StartAsync();

        // 2) Cria a aplicação apontando para o banco do container.
        Factory = new CustomWebApplicationFactory(ConnectionString);

        // 3) Aplica as migrações no banco real (mesma connection string da config).
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // 4) Pronto: cliente HTTP para os testes.
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