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

        // Cenário 1 (GET): o teste insere dados direto no banco e valida a resposta HTTP.
        // Exemplo pronto no Scalar: id=1 (pedido do seed, status PaymentApproved, total 19.90).
        // Outros ids do seed: 2 (refused), 3 (aprovado), 4 (pending), 5 (aprovado), 6 (refused).
        group.MapGet("/{id:int}", async (int id, OrderService service, CancellationToken ct) =>
        {
            var order = await service.GetOrderAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(order);
        })
        .WithName("GetOrder")
        // .NET 10: WithOpenApi foi deprecado (ASPDEPR002); o transformer por endpoint agora é este.
        .AddOpenApiOperationTransformer((operation, _, _) =>
        {
            var idParam = operation.Parameters?.FirstOrDefault(p => p.Name == "id");
            if (idParam is OpenApiParameter p)
            {
                p.Example = JsonNode.Parse("1"); // pedido 1 do seed: aprovado, 2 itens, total 19.90
            }
            return Task.CompletedTask;
        });

        // Cenário 2/3 (POST): cria pedido, cobra no gateway e persiste.
        // Obs.: a SimulatedPaymentGateway SEMPRE aprova em runtime, então todo pedido
        // válido retorna 201. Status recusado/pendente só aparecem via GET (seed).
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

            // Cliente 1 (Ana Souza) + Caneta BIC x2 + Caderno x1 -> total 19.90 (igual ao pedido 1 do seed).
            media.Examples["pedido-valido-201"] = new OpenApiExample
            {
                Summary = "Pedido válido — pagamento aprovado (201)",
                Value = JsonNode.Parse(
                    """{ "customerId": 1, "items": [ { "productId": 1, "quantity": 2 }, { "productId": 2, "quantity": 1 } ] }"""),
            };

            // Cliente 999 não existe no seed -> validação consulta o banco e retorna 400.
            media.Examples["cliente-inexistente-400"] = new OpenApiExample
            {
                Summary = "Cliente inexistente — 400 (nada é persistido)",
                Value = JsonNode.Parse(
                    """{ "customerId": 999, "items": [ { "productId": 1, "quantity": 1 } ] }"""),
            };

            // Produto 999 não existe -> 400 idem.
            media.Examples["produto-inexistente-400"] = new OpenApiExample
            {
                Summary = "Produto inexistente — 400",
                Value = JsonNode.Parse(
                    """{ "customerId": 1, "items": [ { "productId": 999, "quantity": 1 } ] }"""),
            };

            // Exemplo padrão que o Scalar pré-preenche ao abrir o request.
            media.Example = media.Examples["pedido-valido-201"].Value;
            return Task.CompletedTask;
        });

        return app;
    }
}