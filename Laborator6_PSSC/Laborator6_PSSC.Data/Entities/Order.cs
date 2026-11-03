namespace Laborator6_PSSC.Data.Entities;

// Values allowed by CK_Order_Status (SQL/update-db.sql).
public static class OrderStatus
{
    public const string Unpaid = "Unpaid";
    public const string Paid = "Paid";
}

public class Order
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public int CustomerId { get; set; }
    public string Status { get; set; } = OrderStatus.Unpaid;
    public DateTime? PaidAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];
}
