using System.Text.RegularExpressions;

namespace Laborator2_PSSC.Domain.ValueObjects;

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

    public static Result<Address> Create(string? street, string? city, string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(street))
            return Result.Fail<Address>("Strada nu poate fi goala.");
        if (string.IsNullOrWhiteSpace(city))
            return Result.Fail<Address>("Orasul nu poate fi gol.");
        if (postalCode is null || !PostalCodePattern.IsMatch(postalCode.Trim()))
            return Result.Fail<Address>($"Cod postal invalid '{postalCode}'. Trebuie sa aiba 6 cifre.");

        return Result.Ok(new Address(street.Trim(), city.Trim(), postalCode.Trim()));
    }

    public override string ToString() => $"{Street}, {City}, {PostalCode}";
}
