namespace Laborator6_PSSC.Data.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public ICollection<Order> Orders { get; set; } = [];
}
