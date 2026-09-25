using System.Net.Http.Json;
using Demo.Application;
using Demo.Application.Dtos;
using Demo.Domain;
using Demo.Infrastructure;
using Demo.IntegrationTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Tests;

/// <summary>
/// CENÁRIO 2 — "POST pela API → Assert diretamente no banco" e
/// CENÁRIO 3 — "uso de dependência EXTERNA mockável".
///
/// Aqui validamos o EFEITO real da operação: além da resposta HTTP, conferimos
/// o que foi gravado no PostgreSQL usando o MESMO AppDbContext da aplicação.
/// </summary>
[Collection("Integration")]
public class CreateOrderEndpointTests(IntegrationTestFixture fixture)
{
    private readonly IntegrationTestFixture _fixture = fixture;

    /// <summary>Prepara cliente e produto no banco para os testes de criação.</summary>
    private async Task<(int CustomerId, int ProductId, decimal UnitPrice)> ArrangeSeedAsync(AppDbContext db)
    {
        var customer = new Customer { Name = "Bruno Lima", Email = "bruno@exemplo.com" };
        var product = new Product { Name = "Caderno", Price = 15.90m };
        db.Customers.Add(customer);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (customer.Id, product.Id, product.Price);
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_ShouldPersistOrder()
    {
        await _fixture.ResetDatabaseAsync();

        // --- ARRANGE: cliente e produto existem; os dados do pedido vêm do POST.
        int customerId, productId;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (customerId, productId, _) = await ArrangeSeedAsync(db);
        }

        // --- Act: POST pela API, atravessando todas as camadas reais.
        var response = await _fixture.Client.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            items = new[] { new { productId, quantity = 2 } },
        });

        // --- Assert 1: resposta HTTP.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var createdOrderId = int.Parse(response.Headers.Location!.ToString().Split('/').Last());

        // --- Assert 2: efeito real no banco — contexto NOVO + AsNoTracking,
        // garantindo que o dado vem do PostgreSQL e não do ChangeTracker.
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == createdOrderId);

            stored.Should().NotBeNull();
            stored.CustomerId.Should().Be(customerId);
            stored.Status.Should().Be(OrderStatus.PaymentApproved);
            stored.Items.Should().ContainSingle();
            var item = stored.Items.Single();
            item.Quantity.Should().Be(2);
            item.UnitPrice.Should().Be(15.90m);
            stored.Total.Should().Be(31.80m); // 2 × 15,90
        }
    }

    [Fact]
    public async Task CreateOrder_WhenProductDoesNotExist_ShouldReturnBadRequestAndNotPersist()
    {
        await _fixture.ResetDatabaseAsync();

        // --- ARRANGE: apenas o cliente existe.
        int customerId;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (customerId, _, _) = await ArrangeSeedAsync(db);
        }

        // --- Act: POST referenciando um produto que NÃO existe no banco real.
        var response = await _fixture.Client.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            items = new[] { new { productId = 99999, quantity = 1 } },
        });

        // --- Assert: 400 (a validação consultou o PostgreSQL de verdade).
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        // --- ASSERT 2: nada foi persistido.
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Orders.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task CreateOrder_WhenPaymentGatewayApproves_ShouldPersistOrderWithApprovedStatus()
    {
        await _fixture.ResetDatabaseAsync();

        int customerId, productId;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (customerId, productId, _) = await ArrangeSeedAsync(db);
        }

        // --- Arrange: fake que APROVA, injetado via factory (única dependência substituída).
        var fakeGateway = new FakePaymentGateway(approved: true);
        var client = CreateClientWith(fakeGateway);

        // --- Act.
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } },
        });

        // --- Assert 1: 201 e gateway chamado UMA vez com o total correto.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        fakeGateway.Calls.Should().HaveCount(1);
        fakeGateway.Calls[0].Amount.Should().Be(15.90m);
        fakeGateway.Calls[0].CustomerId.Should().Be(customerId);

        // --- Assert 2: banco persiste o pedido com status Aprovado.
        var createdOrderId = int.Parse(response.Headers.Location!.ToString().Split('/').Last());
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == createdOrderId);
            stored.Status.Should().Be(OrderStatus.PaymentApproved);
        }
    }

    [Fact]
    public async Task CreateOrder_WhenPaymentGatewayRefuses_ShouldReturn402AndPersistOrderWithRefusedStatus()
    {
        await _fixture.ResetDatabaseAsync();

        int customerId, productId;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (customerId, productId, _) = await ArrangeSeedAsync(db);
        }

        // --- Arrange: fake que RECUSA.
        var fakeGateway = new FakePaymentGateway(approved: false);
        var client = CreateClientWith(fakeGateway);

        // --- Act.
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            items = new[] { new { productId, quantity = 3 } },
        });

        // --- Assert 1: 402, mas o pedido é persistido para auditoria.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.PaymentRequired);

        // No caso recusado a API não retorna Location; o id vem no corpo.
        var refusedBody = await response.Content.ReadFromJsonAsync<RefusedBody>();

        // --- Assert 2: banco guarda o pedido com status Recusado.
        var createdOrderId = refusedBody!.Order.Id;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == createdOrderId);

            stored.Status.Should().Be(OrderStatus.PaymentRefused);
            stored.Items.Should().ContainSingle();
            stored.Items.Single().Quantity.Should().Be(3);
            stored.Total.Should().Be(47.70m); // 3 × 15,90
        }
    }

    /// <summary>
    /// Cria uma factory/cliente onde o ÚNICO componente substituído é o IPaymentGateway.
    /// Todo o resto (HTTP, ASP.NET, Application, Domain, EF Core, PostgreSQL) é real.
    /// </summary>
    private HttpClient CreateClientWith(FakePaymentGateway fakeGateway)
        => new CustomWebApplicationFactory(_fixture.ConnectionString, fakeGateway).CreateClient();

    private sealed record RefusedBody(OrderResponse Order, string Message);
}