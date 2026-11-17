using System.Text.RegularExpressions;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.ValueObjects;

/// <summary>The student's registration number, as on the student card: "LM" followed by five digits.</summary>
public sealed partial record StudentId
{
    [GeneratedRegex("^LM[0-9]{5}$")]
    private static partial Regex Pattern { get; }

    public string Value { get; }

    private StudentId(string value) => Value = value;

    public static Result<StudentId, StudentIdError> Parse(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant();
        return value is not null && Pattern.IsMatch(value) ? new StudentId(value) : new StudentIdError(raw);
    }

    public override string ToString() => Value;
}

/// <summary>A student id has a single way to be wrong: its format.</summary>
public sealed record StudentIdError(string? Raw);
