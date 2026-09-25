namespace Demo.Domain;

/// <summary>
/// Pedido de compra. O Total é SEMPRE calculado a partir dos itens —
/// nunca persistido no banco. Isso é um ponto da palestra: o cálculo tem que
/// atravessar os itens reais vindos do PostgreSQL num teste de integração.
/// </summary>
public class Order
{
    // Lista inicializada aqui para permitir adicionar itens sem construtor customizado.
    private readonly List<OrderItem> _items = new();

    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; }

    /// <summary>Itens do pedido. EF Core faz binding no backing field "_items".</summary>
    public IReadOnlyCollection<OrderItem> Items => _items;

    /// <summary>Total do pedido, calculado pela soma dos itens (não é mapeado no banco).</summary>
    public decimal Total => _items.Sum(i => i.LineTotal);

    /// <summary>Adiciona um item ao pedido.</summary>
    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A quantidade precisa ser maior que zero.");
        }

        _items.Add(new OrderItem
        {
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = product.Price,
        });
    }
}