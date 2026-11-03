namespace Laborator4_PSSC.Models;

public class Order
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];
}
