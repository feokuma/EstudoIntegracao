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

/// <summary>
/// Resultado do CreateOrder: ou o pedido criado, ou um erro de validação
/// (usado pelo endpoint para decidir entre 201/402 e 400).
/// </summary>
public record CreateOrderResult(bool Success, string? Error, OrderResponse? Order)
{
    public static CreateOrderResult Fail(string error) => new(false, error, null);
    public static CreateOrderResult Ok(OrderResponse order) => new(true, null, order);
}