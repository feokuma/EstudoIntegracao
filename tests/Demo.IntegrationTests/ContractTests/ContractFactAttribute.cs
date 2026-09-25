using Xunit.v3;

namespace Demo.IntegrationTests.ContractTests;

/// <summary>
/// Lançada por um contrato que uma implementação de IAppDbContext não
/// consegue sustentar por CAPACIDADE (não por bug). No xUnit v3, qualquer
/// FactAttribute pode declarar SkipExceptions: a exceção transforma o
/// teste em SKIPPED, com a mensagem como motivo — contraste explícito,
/// sem falso-verde.
/// </summary>
public sealed class PersistenceCapabilityException(string message) : Exception(message);

/// <summary>
/// [Fact] que pula o teste quando a implementação em teste declara não ter
/// capacidade para sustentar o contrato (via PersistenceCapabilityException).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ContractFactAttribute : FactAttribute
{
    public ContractFactAttribute()
    {
        SkipExceptions = [typeof(PersistenceCapabilityException)];
    }
}
