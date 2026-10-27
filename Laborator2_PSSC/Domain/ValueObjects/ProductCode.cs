using System.Text.RegularExpressions;

namespace Laborator2_PSSC.Domain.ValueObjects;

// Format: three letters, dash, four digits (ex: MER-0001).
public sealed record ProductCode
{
    private static readonly Regex Pattern = new(@"^[A-Z]{3}-\d{4}$", RegexOptions.Compiled);

    public string Value { get; }

    private ProductCode(string value) => Value = value;

    public static Result<ProductCode> Create(string? input)
    {
        var value = input?.Trim().ToUpperInvariant();
        return value is not null && Pattern.IsMatch(value)
            ? Result.Ok(new ProductCode(value))
            : Result.Fail<ProductCode>($"Cod produs invalid '{input}'. Format asteptat: ABC-1234.");
    }

    public override string ToString() => Value;
}
