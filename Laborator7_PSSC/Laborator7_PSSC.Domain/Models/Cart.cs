namespace Laborator7_PSSC.Domain.Models;

// A line already in the cart. Code and quantity are kept as stored: carts made in Lab 4 can hold
// products sold by the kilogram, with codes and quantities that today's order rules would not accept.
public sealed record CartLine(string ProductCode, decimal Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

// A placed order is the shopping cart. Closed hierarchy: a cart is either still open for products or paid.
public abstract record Cart
{
    private Cart() { }

    public abstract OrderId Id { get; }
    public abstract IReadOnlyList<CartLine> Lines { get; }

    public decimal Total => Lines.Sum(l => l.LineTotal);

    public sealed record Unpaid(OrderId Id, IReadOnlyList<CartLine> Lines) : Cart
    {
        public override OrderId Id { get; } = Id;
        public override IReadOnlyList<CartLine> Lines { get; } = Lines;
    }

    public sealed record Paid(OrderId Id, IReadOnlyList<CartLine> Lines, DateTime PaidAt) : Cart
    {
        public override OrderId Id { get; } = Id;
        public override IReadOnlyList<CartLine> Lines { get; } = Lines;
    }
}

// Raw input for adding a product, exactly as it comes from outside the domain.
public sealed record UnvalidatedCartItem(string? ProductCode, string? Quantity);

// Facts produced by the cart workflows.
public sealed record ProductAddedToCartEvent(OrderId CartId, ProductCode ProductCode, Quantity Quantity, decimal UnitPrice);

public sealed record CartPaidEvent(OrderId CartId, decimal Total, DateTime PaidAt);

// Why a product could not be added. Closed hierarchy.
public abstract record AddProductToCartError
{
    private AddProductToCartError() { }

    public sealed record InvalidInput(IReadOnlyList<string> Reasons) : AddProductToCartError;
    public sealed record CartNotFound(OrderId CartId) : AddProductToCartError;
    public sealed record CartAlreadyPaid(OrderId CartId) : AddProductToCartError;
    public sealed record ProductNotFound(ProductCode Code) : AddProductToCartError;
    public sealed record InsufficientStock(ProductCode Code, int Requested, int Available) : AddProductToCartError;
}

// Why a cart could not be paid. Closed hierarchy.
public abstract record PayCartError
{
    private PayCartError() { }

    public sealed record CartNotFound(OrderId CartId) : PayCartError;
    public sealed record CartAlreadyPaid(OrderId CartId, DateTime PaidAt) : PayCartError;
    public sealed record CartEmpty(OrderId CartId) : PayCartError;
}
