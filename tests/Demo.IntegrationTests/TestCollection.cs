namespace Demo.IntegrationTests;

/// <summary>
/// Agrupa todos os testes de integração em UMA collection, para que compartilhem
/// o MESMO container PostgreSQL (custo de inicialização, obviamente).
/// As classes dentro da mesma collection rodam em série.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}