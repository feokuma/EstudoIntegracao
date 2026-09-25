using System.Text.Json.Nodes;
using Demo.Application;
using Demo.Application.Dtos;
using Microsoft.OpenApi;

namespace Demo.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (OrderService service, CancellationToken ct) =>
                Results.Ok(await service.GetProductsAsync(ct)))
            .WithName("ListProducts");

        group.MapPost("/", async (CreateProductRequest request, OrderService service, CancellationToken ct) =>
            {
                var result = await service.CreateProductAsync(request, ct);
                return result.Success
                    ? Results.Created($"/api/products/{result.Product!.Id}", result.Product)
                    : Results.Conflict(new { error = result.Error });
            })
            .WithName("CreateProduct")
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

                media.Examples["produto-novo-201"] = new OpenApiExample
                {
                    Summary = "Produto novo — 201",
                    Value = JsonNode.Parse("""{ "name": "Tesoura Escolar", "price": 7.99 }"""),
                };

                media.Examples["nome-duplicado-409"] = new OpenApiExample
                {
                    Summary = "Nome duplicado — 409 (regra #5)",
                    Value = JsonNode.Parse("""{ "name": "Caneta BIC", "price": 9.99 }"""),
                };

                media.Example = media.Examples["produto-novo-201"].Value;
                return Task.CompletedTask;
            });

        return app;
    }
}