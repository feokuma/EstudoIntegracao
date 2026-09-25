using System.Text.Json.Nodes;
using Demo.Application;
using Demo.Application.Dtos;
using Demo.Domain;
using Microsoft.OpenApi;

namespace Demo.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        // Cenário 1 (GET): o teste organiza os dados direto no banco e valida a resposta HTTP.
        group.MapGet("/{id:int}", async (int id, OrderService service, CancellationToken ct) =>
        {
            var order = await service.GetOrderAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(order);
        })
        .WithName("GetOrder")
        .AddOpenApiOperationTransformer((operation, _, _) =>
        {
            var idParam = operation.Parameters?.FirstOrDefault(p => p.Name == "id");
            if (idParam is OpenApiParameter p)
            {
                p.Example = JsonNode.Parse("1");
            }
            return Task.CompletedTask;
        });

        // Cenários 2/3 (POST): em runtime o gateway simulado sempre aprova (201);
        // status recusado só aparece via GET no seed — nos testes, via fake do gateway.
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
        })
        .WithName("CreateOrder")
        .AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation.RequestBody is null)
            {
                return Task.CompletedTask;
            }

            // A interface IOpenApiRequestBody é read-only; o tipo concreto tem setter.
            var body = (OpenApiRequestBody)operation.RequestBody;
            var content = body.Content ??= new Dictionary<string, OpenApiMediaType>();
            content.TryAdd("application/json", new OpenApiMediaType());
            var media = content["application/json"]!;
            media.Examples ??= new Dictionary<string, IOpenApiExample>();

            media.Examples["pedido-valido-201"] = new OpenApiExample
            {
                Summary = "Pedido válido — pagamento aprovado (201)",
                Value = JsonNode.Parse(
                    """{ "customerId": 1, "items": [ { "productId": 1, "quantity": 2 }, { "productId": 2, "quantity": 1 } ] }"""),
            };

            media.Examples["cliente-inexistente-400"] = new OpenApiExample
            {
                Summary = "Cliente inexistente — 400 (nada é persistido)",
                Value = JsonNode.Parse(
                    """{ "customerId": 999, "items": [ { "productId": 1, "quantity": 1 } ] }"""),
            };

            media.Examples["produto-inexistente-400"] = new OpenApiExample
            {
                Summary = "Produto inexistente — 400",
                Value = JsonNode.Parse(
                    """{ "customerId": 1, "items": [ { "productId": 999, "quantity": 1 } ] }"""),
            };

            media.Example = media.Examples["pedido-valido-201"].Value;
            return Task.CompletedTask;
        });

        return app;
    }
}