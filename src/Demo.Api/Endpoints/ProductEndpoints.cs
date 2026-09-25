using Demo.Application;
using Demo.Application.Dtos;

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
            .WithName("CreateProduct");

        return app;
    }
}