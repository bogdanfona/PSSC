using Laborator2_PSSC.Domain.ValueObjects;

namespace Laborator2_PSSC.Domain.Entities;

public sealed class Customer : Entity<CustomerId>
{
    public string Name { get; }
    public Address Address { get; }

    private Customer(CustomerId id, string name, Address address) : base(id)
    {
        Name = name;
        Address = address;
    }

    public static Result<Customer> Create(string? name, Address address) =>
        string.IsNullOrWhiteSpace(name)
            ? Result.Fail<Customer>("Numele clientului nu poate fi gol.")
            : Result.Ok(new Customer(CustomerId.New(), name.Trim(), address));

    public override string ToString() => $"{Name} ({Address})";
}
