using Demo.Application;
using Demo.Domain;
using FluentAssertions;

namespace Demo.UnitTests;

/// <summary>
/// Testes de UNIDADE do OrderService usando o FakeAppDbContext (EF InMemory).
/// Eles validam lógica em memória e funcionam mesmo quando a persistência está
/// quebrada — é exatamente o que queremos demonstrar ao vivo (contraste com a
/// suíte de integração, que roda contra PostgreSQL real).
/// </summary>
public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrder_WithValidRequest_ShouldCalculateCorrectTotalAndCallGateway()
    {
        // Arrange
        var customer = new Customer { Id = 1, Name = "Ana", Email = "ana@x.com" };
        var product = new Product { Id = 1, Name = "Caneta", Price = 10.5m };
        var db = FakeAppDbContext.Create(
            customers: [customer],
            products: [product]);

        var gateway = new SpyPaymentGateway();
        var service = new OrderService(db, gateway);

        var request = new Application.Dtos.CreateOrderRequest(
            CustomerId: 1,
            Items: [new Application.Dtos.CreateOrderItemRequest(ProductId: 1, Quantity: 3)]);

        // Act
        var result = await service.CreateOrderAsync(request);

        // Assert — apenas sobre o valor de retorno (nada de conferir o "banco").
        result.Success.Should().BeTrue();
        result.Order!.Total.Should().Be(31.5m);
        result.Order.Items.Should().HaveCount(1);
        // O gateway externo foi chamado com o total correto.
        gateway.Calls.Should().HaveCount(1);
        gateway.Calls[0].Amount.Should().Be(31.5m);
    }

    [Fact]
    public async Task CreateOrder_WhenProductDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var customer = new Customer { Id = 1, Name = "Ana", Email = "ana@x.com" };
        var db = FakeAppDbContext.Create(customers: [customer]);
        var service = new OrderService(db, new SpyPaymentGateway());

        var request = new Application.Dtos.CreateOrderRequest(
            CustomerId: 1,
            Items: [new Application.Dtos.CreateOrderItemRequest(ProductId: 999, Quantity: 1)]);

        // Act
        var result = await service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Order.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrder_WhenPaymentGatewayRefuses_ShouldSetRefusedStatus()
    {
        // Arrange
        var customer = new Customer { Id = 1, Name = "Ana", Email = "ana@x.com" };
        var product = new Product { Id = 1, Name = "Caneta", Price = 5m };
        var db = FakeAppDbContext.Create(customers: [customer], products: [product]);
        var service = new OrderService(db, new SpyPaymentGateway(approved: false));

        var request = new Application.Dtos.CreateOrderRequest(
            CustomerId: 1,
            Items: [new Application.Dtos.CreateOrderItemRequest(ProductId: 1, Quantity: 1)]);

        // Act
        var result = await service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Order!.Status.Should().Be(nameof(OrderStatus.PaymentRefused));
    }

    // Regra #5: validação no service (AnyAsync no fake). Passam mesmo no branch de
    // demonstração — o fake em memória nunca impõe a constraint; a garantia real
    // está só no banco (PostgreSQL), verificada pelos testes de integração.
    [Fact]
    public async Task CreateCustomer_WhenEmailAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var db = FakeAppDbContext.Create(customers: [new Customer { Name = "Ana", Email = "ana@x.com" }]);
        var service = new OrderService(db, new SpyPaymentGateway());

        // Act: mesmo e-mail de um cliente já existente.
        var result = await service.CreateCustomerAsync(new Application.Dtos.CreateCustomerRequest("Outra", "ana@x.com"));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("ana@x.com");
    }

    [Fact]
    public async Task CreateProduct_WhenNameAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var db = FakeAppDbContext.Create(products: [new Product { Name = "Caneta", Price = 3m }]);
        var service = new OrderService(db, new SpyPaymentGateway());

        // Act: mesmo nome de um produto já existente.
        var result = await service.CreateProductAsync(new Application.Dtos.CreateProductRequest("Caneta", 4m));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Caneta");
    }

    /// <summary>Fake simples do gateway: registra as chamadas e configura a aprovação.</summary>
    private sealed class SpyPaymentGateway(bool approved = true) : IPaymentGateway
    {
        public List<(int CustomerId, decimal Amount)> Calls { get; } = [];

        public Task<PaymentResult> ChargeAsync(Customer customer, decimal amount, CancellationToken cancellationToken = default)
        {
            Calls.Add((customer.Id, amount));
            return Task.FromResult(
                approved ? PaymentResult.Approved("unit-tx") : PaymentResult.Refused("saldo insuficiente"));
        }
    }
}