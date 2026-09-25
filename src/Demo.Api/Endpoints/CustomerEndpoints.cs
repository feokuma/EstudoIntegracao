using System.Text.Json.Nodes;
using Demo.Application;
using Demo.Application.Dtos;
using Microsoft.OpenApi;

namespace Demo.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/customers")
            .MapPost("/", async (CreateCustomerRequest request, OrderService service, CancellationToken ct) =>
            {
                var result = await service.CreateCustomerAsync(request, ct);
                return result.Success
                    ? Results.Created($"/api/customers/{result.Customer!.Id}", result.Customer)
                    : Results.Conflict(new { error = result.Error });
            })
            .WithTags("Customers")
            .WithName("CreateCustomer")
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

                media.Examples["cliente-novo-201"] = new OpenApiExample
                {
                    Summary = "Cliente novo — 201",
                    Value = JsonNode.Parse("""{ "name": "Gabriela Nunes", "email": "gabi@exemplo.com" }"""),
                };

                media.Examples["email-duplicado-409"] = new OpenApiExample
                {
                    Summary = "E-mail duplicado — 409 (regra #5)",
                    Value = JsonNode.Parse("""{ "name": "Ana Souza", "email": "ana@exemplo.com" }"""),
                };

                media.Example = media.Examples["cliente-novo-201"].Value;
                return Task.CompletedTask;
            });

        return app;
    }
}