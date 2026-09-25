using System.Net.Http.Json;
using Demo.Application.Dtos;
using Demo.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Tests;

/// <summary>
/// Regra de negócio #5 — unicidade de e-mail (cliente) e nome (produto).
///
/// Duas frentes:
///   1. Via API: o service valida duplicidade e devolve 409 (checagem em tempo de request).
///   2. Via banco: o ÍNDICE ÚNICO real é a garantia definitiva. Inserir um registro
///      duplicado DIRETO no PostgreSQL lança erro. Um fake em memória (teste de unidade)
///      NUNCA consegue verificar isso — só o banco real.
///
/// O teste *_EnforcedByDatabase (produto) é o que "quebra" no branch de demonstração,
/// onde o índice único foi (propositadamente) esquecido.
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

    // Regra #5 via BANCO: o índice único do PostgreSQL é a garantia real —
    // um fake em memória (teste de unidade) nunca consegue verificar isso.
    [Fact]
    public async Task CustomerEmail_UniqueConstraint_EnforcedByDatabase()
    {
        await _fixture.ResetDatabaseAsync();

        var email = $"db-{Guid.NewGuid():N}@exemplo.com";

        // 1ª inserção direta: ok.
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new Domain.Customer { Name = "Primeiro", Email = email });
            await db.SaveChangesAsync();
        }

        // 2ª inserção direta com o MESMO e-mail: o índice único do PostgreSQL rejeita.
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new Domain.Customer { Name = "Duplicado", Email = email });

            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    /// <summary>Regra #5 via BANCO (bug didático: discriminador entre os branchs).</summary>
    [Fact]
    public async Task ProductName_UniqueConstraint_EnforcedByDatabase()
    {
        await _fixture.ResetDatabaseAsync();

        var name = $"Produto-DB-{Guid.NewGuid():N}";

        // 1ª inserção direta: ok.
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Domain.Product { Name = name, Price = 1m });
            await db.SaveChangesAsync();
        }

        // 2ª inserção direta com o MESMO nome: depende do índice único (`AddProductNameUniqueIndex`).
        //  - main: índice existe → rejeita (DbUpdateException). ✅
        //  - demo/unit-vs-integration: índice omitido (bug) → grava duplicado → o teste FALHA. ❌
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Domain.Product { Name = name, Price = 2m });

            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}