namespace Laborator4_PSSC.Models;

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public ICollection<Order> Orders { get; set; } = [];
}
