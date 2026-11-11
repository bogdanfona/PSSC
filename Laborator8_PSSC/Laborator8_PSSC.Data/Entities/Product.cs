namespace Laborator8_PSSC.Data.Entities;

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string QuantityType { get; set; } = "";
    public int Stock { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
