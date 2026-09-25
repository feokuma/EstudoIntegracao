using Demo.Application;
using Demo.Application.Dtos;
using Demo.Domain;

namespace Demo.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        // Cenário 1 (GET): o teste insere dados direto no banco e valida a resposta HTTP.
        group.MapGet("/{id:int}", async (int id, OrderService service, CancellationToken ct) =>
        {
            var order = await service.GetOrderAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(order);
        }).WithName("GetOrder");

        // Cenário 2/3 (POST): cria pedido, cobra no gateway e persiste.
        group.MapPost("/", async (CreateOrderRequest request, OrderService service, CancellationToken ct) =>
        {
            var result = await service.CreateOrderAsync(request, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return result.Order!.Status switch
            {
                // Pagamento aprovado -> 201 com Location para o recurso criado.
                nameof(OrderStatus.PaymentApproved) =>
                    Results.Created($"/api/orders/{result.Order.Id}", result.Order),

                // Pagamento recusado -> o pedido É persistido (auditoria), resposta 402.
                nameof(OrderStatus.PaymentRefused) =>
                    Results.Json(new { order = result.Order, message = "Pagamento recusado." }, statusCode: StatusCodes.Status402PaymentRequired),

                _ => Results.Ok(result.Order),
            };
        }).WithName("CreateOrder");

        return app;
    }
}