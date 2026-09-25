using System.Net.Http.Json;
using Demo.Application.Dtos;
using Demo.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Tests;

/// <summary>
/// Regra de negócio #5 — unicidade de e-mail (cliente) e nome (produto).
///
/// Via API: o service valida duplicidade e devolve 409 (checagem em tempo de request).
///
/// A camada defensiva PELO BANCO (índice único, insert duplicado direto no
/// PostgreSQL lança DbUpdateException) NÃO é testada aqui — ela vive na suíte de
/// CONTRATO (ver AppDbContextContractTests: C3/C4), que a valida no lugar certo,
/// contra `IAppDbContext`, sem HTTP.
///
/// O contrato C4 é o discriminador dos branches de demonstração/estudo: no branch
/// demo/unit-vs-integration o índice único foi (propositadamente) esquecido e
/// `Product_NameUniqueIndex_IsEnforcedByPersistence` falha.
/// </summary>
[Collection("Integration")]
public class CatalogEndpointTests(IntegrationTestFixture fixture)
{
    private readonly IntegrationTestFixture _fixture = fixture;

    [Fact]
    public async Task CreateCustomer_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        await _fixture.ResetDatabaseAsync();

        // Arrange: cliente com o e-mail que será usado no POST já existe.
        var email = $"dupe-{Guid.NewGuid():N}@exemplo.com";
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new Domain.Customer { Name = "Ana", Email = email });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("Outra Pessoa", email));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateProduct_WhenNameAlreadyExists_ShouldReturnConflict()
    {
        await _fixture.ResetDatabaseAsync();

        var name = $"Produto-Seed-{Guid.NewGuid():N}";
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Domain.Product { Name = name, Price = 1.99m });
            await db.SaveChangesAsync();
        }

        var response = await _fixture.Client.PostAsJsonAsync("/api/products",
            new CreateProductRequest(name, 2.99m));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }
}