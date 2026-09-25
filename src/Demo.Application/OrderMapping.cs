using Demo.Domain;
using Demo.Application.Dtos;

namespace Demo.Application;

/// <summary>Conversão explícita entre entidades do domínio e DTOs (sem AutoMapper).</summary>
public static class OrderMapping
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Customer?.Name ?? "?",
        order.Status.ToString(),
        order.Total,
        order.CreatedAt,
        order.Items.Select(i => new OrderItemResponse(
            i.ProductId,
            i.Product?.Name ?? "?",
            i.Quantity,
            i.UnitPrice)).ToList());
}