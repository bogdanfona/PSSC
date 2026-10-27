using System.Text.RegularExpressions;

namespace Laborator3_PSSC.Domain.Models;

public sealed record CustomerName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private CustomerName(string value) => Value = value;

    public static Result<CustomerName, string> Create(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
            return new Result<CustomerName, string>.Failure("Numele clientului nu poate fi gol.");
        if (value.Length > MaxLength)
            return new Result<CustomerName, string>.Failure($"Numele clientului poate avea cel mult {MaxLength} caractere.");
        return new Result<CustomerName, string>.Success(new CustomerName(value));
    }

    public override string ToString() => Value;
}

// Format: three letters, dash, four digits (ex: MER-0001).
public sealed record ProductCode
{
    private static readonly Regex Pattern = new(@"^[A-Z]{3}-\d{4}$", RegexOptions.Compiled);

    public string Value { get; }

    private ProductCode(string value) => Value = value;

    public static Result<ProductCode, string> Create(string? input)
    {
        var value = input?.Trim().ToUpperInvariant();
        return value is not null && Pattern.IsMatch(value)
            ? new Result<ProductCode, string>.Success(new ProductCode(value))
            : new Result<ProductCode, string>.Failure($"Cod produs invalid '{input}'. Format asteptat: ABC-1234.");
    }

    public override string ToString() => Value;
}

public sealed record Quantity
{
    public const int Min = 1;
    public const int Max = 100;

    public int Value { get; }

    private Quantity(int value) => Value = value;

    public static Result<Quantity, string> Parse(string? input) =>
        int.TryParse(input?.Trim(), out var value) && value is >= Min and <= Max
            ? new Result<Quantity, string>.Success(new Quantity(value))
            : new Result<Quantity, string>.Failure($"Cantitate invalida '{input}'. Trebuie sa fie intre {Min} si {Max}.");

    public override string ToString() => Value.ToString();
}

public sealed record Address
{
    private static readonly Regex PostalCodePattern = new(@"^\d{6}$", RegexOptions.Compiled);

    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }

    private Address(string street, string city, string postalCode)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
    }

    // Returns every problem found, not just the first one.
    public static Result<Address, IReadOnlyList<string>> Create(string? street, string? city, string? postalCode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(street))
            errors.Add("Strada nu poate fi goala.");
        if (string.IsNullOrWhiteSpace(city))
            errors.Add("Orasul nu poate fi gol.");
        if (postalCode is null || !PostalCodePattern.IsMatch(postalCode.Trim()))
            errors.Add($"Cod postal invalid '{postalCode}'. Trebuie sa aiba 6 cifre.");

        return errors.Count == 0
            ? new Result<Address, IReadOnlyList<string>>.Success(new Address(street!.Trim(), city!.Trim(), postalCode!.Trim()))
            : new Result<Address, IReadOnlyList<string>>.Failure(errors);
    }

    public override string ToString() => $"{Street}, {City}, {PostalCode}";
}

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString()[..8];
}
