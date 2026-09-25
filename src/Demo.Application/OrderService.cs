using Microsoft.EntityFrameworkCore;
using Demo.Domain;
using Demo.Application.Dtos;

namespace Demo.Application;

/// <summary>
/// Casos de uso da loja. Código EXPLÍCITO, sem MediatR/AutoMapper/repos:
/// o teste de integração consegue enxergar cada query no banco e cada chamada ao gateway.
/// </summary>
public class OrderService(IAppDbContext db, IPaymentGateway paymentGateway)
{
    /// <summary>
    /// GET /api/orders/{id} — carrega o pedido com cliente e itens (Include real do EF Core).
    /// Retorna null quando não existe (o endpoint converte em 404).
    /// </summary>
    public async Task<OrderResponse?> GetOrderAsync(int id, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : order.ToResponse();
    }

    /// <summary>
    /// POST /api/orders — valida cliente/produtos no banco real, monta o pedido,
    /// cobra no gateway e faz o PERSIST, com o status vindo da resposta do gateway.
    /// </summary>
    public async Task<CreateOrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // Aprovação: bater no banco real — nada de in-memory aqui.
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
        {
            return CreateOrderResult.Fail($"Cliente {request.CustomerId} não encontrado.");
        }

        var requestedProductIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => requestedProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        if (products.Count != requestedProductIds.Count)
        {
            var missing = string.Join(",", requestedProductIds.Where(id => !products.ContainsKey(id)));
            return CreateOrderResult.Fail($"Produto(s) inexistente(s): {missing}.");
        }

        var order = new Order
        {
            CustomerId = customer.Id,
            Customer = customer,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var item in request.Items)
        {
            order.AddItem(products[item.ProductId], item.Quantity);
        }

        // Chamada à dependência EXTERNA. Em runtime usa o SimulatedPaymentGateway;
        // nos testes pode ser um fake que aprova ou recusa.
        var payment = await paymentGateway.ChargeAsync(customer, order.Total, ct);
        order.Status = payment.IsApproved
            ? OrderStatus.PaymentApproved
            : OrderStatus.PaymentRefused;

        db.Orders.Add(order);

        // Persiste tudo (pedido + itens) no PostgreSQL. É aqui que a aplicação
        // "commita" o estado. Se faltar, nada é gravado no banco.
        await db.SaveChangesAsync(ct);

        return CreateOrderResult.Ok(order.ToResponse());
    }

    /// <summary>Lista produtos (suporte para GET /api/products).</summary>
    public async Task<List<ProductResponse>> GetProductsAsync(CancellationToken ct = default)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductResponse(p.Id, p.Name, p.Price))
            .ToListAsync(ct);
    }
}