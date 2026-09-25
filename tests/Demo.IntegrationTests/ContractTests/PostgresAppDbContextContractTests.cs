using Demo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Demo.IntegrationTests.ContractTests;

/// <summary>
/// Executa a suíte de contrato (AppDbContextContractTests) contra o lado REAL:
/// AppDbContext (Npgsql) + PostgreSQL via Testcontainers. Aplica as migrations
/// exatamente como a aplicação faria em produção.
///
/// É esta execução que detecta:
///   • índice único "esquecido" na migration (demo/unit-vs-integration)
///   • schema dessincronizado com o modelo
/// </summary>
[Collection(DbContextContractCollection.Id)]
public class PostgresAppDbContextContractTests(DbContextContractFixture fixture) : AppDbContextContractTests
{
    protected override Task<PersistContext> CreateContextAsync()
        => Task.FromResult(new PersistContext(fixture.CreateContext()));

    protected override async Task ResetDataAsync() => await fixture.ResetDatabaseAsync();
}
