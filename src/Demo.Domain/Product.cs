namespace Demo.Domain;

/// <summary>Produto disponível para compra. O preço é capturado no item quando o pedido é criado.</summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}