using Demo.Application;
using Demo.Application.Dtos;

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
            .WithName("CreateCustomer");

        return app;
    }
}