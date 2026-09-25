namespace Demo.Application.Dtos;

public record CreateOrderRequest(int CustomerId, IReadOnlyList<CreateOrderItemRequest> Items);

public record CreateOrderItemRequest(int ProductId, int Quantity);

public record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderResponse(
    int Id,
    int CustomerId,
    string CustomerName,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemResponse> Items);

public record ProductResponse(int Id, string Name, decimal Price);

public record CreateCustomerRequest(string Name, string Email);

public record CustomerResponse(int Id, string Name, string Email);

public record CreateProductRequest(string Name, decimal Price);

/// <summary>Resultado da criação de um cliente (regra #5: email único).</summary>
public record CreateCustomerResult(bool Success, string? Error, CustomerResponse? Customer)
{
    public static CreateCustomerResult Fail(string error) => new(false, error, null);
    public static CreateCustomerResult Ok(CustomerResponse customer) => new(true, null, customer);
}

/// <summary>Resultado da criação de um produto (regra #5: nome único).</summary>
public record CreateProductResult(bool Success, string? Error, ProductResponse? Product)
{
    public static CreateProductResult Fail(string error) => new(false, error, null);
    public static CreateProductResult Ok(ProductResponse product) => new(true, null, product);
}

/// <summary>
/// Resultado do CreateOrder: ou o pedido criado, ou um erro de validação
/// (usado pelo endpoint para decidir entre 201/402 e 400).
/// </summary>
public record CreateOrderResult(bool Success, string? Error, OrderResponse? Order)
{
    public static CreateOrderResult Fail(string error) => new(false, error, null);
    public static CreateOrderResult Ok(OrderResponse order) => new(true, null, order);
}