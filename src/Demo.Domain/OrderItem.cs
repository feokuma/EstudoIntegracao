namespace Demo.Domain;

/// <summary>
/// Item de um pedido. Guarda uma cópia do preço (UnitPrice) no momento da compra,
/// para que mudanças de preço no produto não alterem o fechamento do pedido.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}