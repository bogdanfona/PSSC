namespace Laborator5_PSSC.Data.Entities;

public class Order
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];
}
