using Demo.UnitTests;
using Xunit.v3;

namespace Demo.IntegrationTests.ContractTests;

/// <summary>
/// Executa a MESMA suíte de contrato contra o lado FAKE: FakeAppDbContext
/// (EF Core InMemory). Cada contrato roda de verdade — e os que o fake é
/// GENUINAMENTE incapaz de sustentar (índice único, NOT NULL físico — o
/// provider InMemory sequer avalia HasIndex/constraints) caem como
/// SKIPPED com motivo legível, via PersistenceCapabilityException +
/// <see cref="ContractFactAttribute"/>. Sem falso-verde: o contraste
/// entre as duas execuções fica explícito na saída da suíte.
/// </summary>
[Collection(DbContextContractCollection.Id)]
public class FakeAppDbContextContractTests : AppDbContextContractTests
{
    private string? _sharedDatabaseName;

    protected override Task<PersistContext> CreateContextAsync()
    {
        // Nome de banco COMPARTILHADO entre as duas instâncias do C1/C2:
        // simula o mesmo cenário do real (duas visões de um mesmo banco).
        _sharedDatabaseName ??= $"contract-fake-{Guid.NewGuid():N}";
        return Task.FromResult(new PersistContext(FakeAppDbContext.Create(databaseName: _sharedDatabaseName)));
    }

    protected override Task ResetDataAsync()
    {
        // O fake usa bancos efêmeros (nome único por Create()), então cada
        // teste já está isolado — não há o que resetar.
        return Task.CompletedTask;
    }

    public override async Task Customer_EmailUniqueIndex_IsEnforcedByPersistence()
    {
        // O provider InMemory NÃO impõe índices únicos: nada será arremessado
        // pelo fake → sem skip automático por exceção, declaramos a capacidade.
        throw new PersistenceCapabilityException(
            "O provider InMemory não impõe índices únicos — regra #5 exige PostgreSQL real");
    }

    public override Task Product_NameUniqueIndex_IsEnforcedByPersistence()
    {
        throw new PersistenceCapabilityException(
            "O provider InMemory não impõe índices únicos — regra #5 exige PostgreSQL real");
    }

    public override Task Customer_SavingWithoutEmail_IsRejectedByPersistence()
    {
        throw new PersistenceCapabilityException(
            "Coluna NOT NULL física é garantia de schema, exclusiva do PostgreSQL");
    }
}
