using Demo.Application;

namespace Demo.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/products")
            .MapGet("/", async (OrderService service, CancellationToken ct) =>
                Results.Ok(await service.GetProductsAsync(ct)))
            .WithTags("Products")
            .WithName("ListProducts");

        return app;
    }
}