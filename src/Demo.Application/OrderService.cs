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
        // A validação consulta o banco real — nada de in-memory aqui.
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

        // Persiste tudo (pedido + itens) no PostgreSQL. Se faltar, nada é gravado
        // no banco — e só o teste de integração percebe.
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

    /// <summary>
    /// POST /api/customers — Regra #5: email ÚNICO.
    /// A regra é validada aqui (camada de aplicação) e reforçada por índice único no banco.
    /// </summary>
    public async Task<CreateCustomerResult> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var duplicate = await db.Customers.AnyAsync(c => c.Email == request.Email, ct);
        if (duplicate)
        {
            return CreateCustomerResult.Fail($"Já existe um cliente com o e-mail {request.Email}.");
        }

        var customer = new Customer { Name = request.Name, Email = request.Email };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return CreateCustomerResult.Ok(new CustomerResponse(customer.Id, customer.Name, customer.Email));
    }

    /// <summary>
    /// POST /api/products — Regra #5: nome do produto ÚNICO.
    /// Igual ao cliente: validação no service + índice único no banco (a garantia real).
    /// </summary>
    public async Task<CreateProductResult> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var duplicate = await db.Products.AnyAsync(p => p.Name == request.Name, ct);
        if (duplicate)
        {
            return CreateProductResult.Fail($"Já existe um produto com o nome {request.Name}.");
        }

        var product = new Product { Name = request.Name, Price = request.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return CreateProductResult.Ok(new ProductResponse(product.Id, product.Name, product.Price));
    }
}