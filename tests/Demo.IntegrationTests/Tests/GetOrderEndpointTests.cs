using System.Net.Http.Json;
using Demo.Application.Dtos;
using Demo.Domain;
using Demo.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Tests;

/// <summary>
/// Cenário 1 — "Arrange direto no banco → chamada HTTP → Assert da API".
/// O estado inicial do banco é controlado inserindo dados via DbContext,
/// sem passar pela própria API.
/// </summary>
[Collection("Integration")]
public class GetOrderEndpointTests(IntegrationTestFixture fixture)
{
    private readonly IntegrationTestFixture _fixture = fixture;

    [Fact]
    public async Task GetOrder_WhenOrderExists_ShouldReturnOrderWithItemsAndTotal()
    {
        // Garante um banco limpo para este teste.
        await _fixture.ResetDatabaseAsync();

        // --- Arrange: insere cliente + produto + pedido DIRETAMENTE no PostgreSQL.
        int createdOrderId;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var customer = new Customer { Name = "Ana Souza", Email = "ana@exemplo.com" };
            var product = new Product { Name = "Caneta", Price = 10.50m };
            db.Customers.Add(customer);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var order = new Order
            {
                CustomerId = customer.Id,
                Customer = customer,
                Status = OrderStatus.PaymentApproved,
                CreatedAt = DateTime.UtcNow,
            };
            order.AddItem(product, 3); // 3 × 10,50 = 31,50
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            createdOrderId = order.Id;
        }

        // --- Act: chamada HTTP real (ASP.NET → Application → EF Core → PostgreSQL).
        var response = await _fixture.Client.GetAsync($"/api/orders/{createdOrderId}");

        // --- Assert: status e conteúdo calculados pela aplicação real.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.Id.Should().Be(createdOrderId);
        body.CustomerName.Should().Be("Ana Souza");
        body.Items.Should().HaveCount(1);
        body.Items[0].Quantity.Should().Be(3);
        body.Items[0].UnitPrice.Should().Be(10.50m);
        body.Total.Should().Be(31.50m);
    }

    [Fact]
    public async Task GetOrder_WhenOrderDoesNotExist_ShouldReturnNotFound()
    {
        await _fixture.ResetDatabaseAsync();

        // --- Act: id inexistente (banco vazio neste cenário).
        var response = await _fixture.Client.GetAsync("/api/orders/999");

        // --- Assert.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Regra de negócio #6 — o preço do pedido é "congelado" no ato da compra.
    /// Alterar o preço do produto DEPOIS não pode mudar o total de pedidos já criados.
    ///
    /// Discriminador do bug didático: se o total usar o preço ATUAL do produto (em vez
    /// do UnitPrice gravado), este teste falha — e nenhum teste de unidade percebe,
    /// porque mudanças de preço só acontecem no fluxo real (banco).
    /// </summary>
    [Fact]
    public async Task GetOrder_WhenProductPriceChangesAfterPurchase_ShouldKeepOriginalUnitPrice()
    {
        // Garante banco limpo.
        await _fixture.ResetDatabaseAsync();

        // --- Arrange: produto (R$ 10,50) e um pedido com 3 unidades = total R$ 31,50.
        int orderId, productId;
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var customer = new Customer { Name = "Ana Souza", Email = "ana-frozen@exemplo.com" };
            var product = new Product { Name = $"Caneta-Frozen-{Guid.NewGuid():N}", Price = 10.50m };
            db.Customers.Add(customer);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var order = new Order
            {
                CustomerId = customer.Id,
                Customer = customer,
                Status = OrderStatus.PaymentApproved,
                CreatedAt = DateTime.UtcNow,
            };
            order.AddItem(product, 3);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            orderId = order.Id;
            productId = product.Id;
        }

        // --- Pré-arrange: depois da compra, o preço do produto sobe para R$ 88,00 no banco.
        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.FindAsync(productId);
            product!.Price = 88.00m;
            await db.SaveChangesAsync();
        }

        // --- Act: GET do pedido criado ANTES da mudança de preço.
        var response = await _fixture.Client.GetAsync($"/api/orders/{orderId}");

        // --- Assert: total deve continuar o "congelado" (3 × 10,50), NÃO o preço novo.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();

        body!.Items.Should().ContainSingle();
        body.Items[0].UnitPrice.Should().Be(10.50m); // preço do ato da compra
        body.Total.Should().Be(31.50m);              // 3 × 10,50 (e NÃO 3 × 88,00)
    }
}