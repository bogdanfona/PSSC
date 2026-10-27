using System.Collections.Immutable;
using Laborator2_PSSC.Domain.Entities;
using Laborator2_PSSC.Domain.ValueObjects;

namespace Laborator2_PSSC.Domain.States;

// Closed hierarchy of cart states: the private constructor means only the nested
// types below can derive from Cart. Every state keeps the same CartId, so the cart
// stays the same entity as it moves through its lifecycle.
// Each state only exposes the transitions that are allowed from it.
public abstract class Cart : Entity<CartId>, ICart
{
    public Customer Customer { get; }

    private Cart(CartId id, Customer customer) : base(id) => Customer = customer;

    public static Empty CreateEmpty(Customer customer) => new(CartId.New(), customer);

    public sealed class Empty : Cart
    {
        internal Empty(CartId id, Customer customer) : base(id, customer) { }

        public Unvalidated AddItem(CartItem item) => new(Id, Customer, [item]);
    }

    public sealed class Unvalidated : Cart
    {
        public ImmutableList<CartItem> Items { get; }

        internal Unvalidated(CartId id, Customer customer, ImmutableList<CartItem> items)
            : base(id, customer) => Items = items;

        public Unvalidated AddItem(CartItem item) => new(Id, Customer, Items.Add(item));

        public Validated Validate() => new(Id, Customer, Items, Customer.Address);
    }

    public sealed class Validated : Cart
    {
        public ImmutableList<CartItem> Items { get; }
        public Address ShippingAddress { get; }

        internal Validated(CartId id, Customer customer, ImmutableList<CartItem> items, Address shippingAddress)
            : base(id, customer)
        {
            Items = items;
            ShippingAddress = shippingAddress;
        }

        public Paid Pay(DateTime paidAt) => new(Id, Customer, Items, ShippingAddress, paidAt);
    }

    public sealed class Paid : Cart
    {
        public ImmutableList<CartItem> Items { get; }
        public Address ShippingAddress { get; }
        public DateTime PaidAt { get; }

        internal Paid(CartId id, Customer customer, ImmutableList<CartItem> items, Address shippingAddress, DateTime paidAt)
            : base(id, customer)
        {
            Items = items;
            ShippingAddress = shippingAddress;
            PaidAt = paidAt;
        }
    }
}
