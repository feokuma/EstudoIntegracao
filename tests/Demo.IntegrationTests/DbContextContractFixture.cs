using Demo.Application;
using Demo.Domain;
using Demo.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Demo.IntegrationTests;

/// <summary>
/// Fixture que sobe UM PostgreSQL (postgres:17) para os testes de CONTRATO
/// entre a Application (IAppDbContext) e a Infrastructure (AppDbContext).
///
/// Aqui NÃO há HTTP nem WebApplicationFactory: a suíte exercita o espaço
/// mínimo capaz de quebrar o contrato — DbContext real + container.
/// </summary>
public sealed class DbContextContractFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Cria um AppDbContext novo e isolado (o contrato vale ENTRE instâncias:
    /// o que uma instância persiste deve ser visível à outra).
    /// </summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "Customers", "Products", "Orders", "OrderItems" RESTART IDENTITY CASCADE;""");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(DbContextContractCollection.Id)]
public class DbContextContractCollection : ICollectionFixture<DbContextContractFixture>
{
    public const string Id = "DbContextContract";
}
