namespace Laborator9_PSSC.Domain.Errors;

/// <summary>Why the reserve space workflow did not place a reservation. Closed hierarchy; not an event, not a state.</summary>
public abstract record ReserveSpaceError
{
    private ReserveSpaceError() { }

    /// <summary>Some fields are malformed or the space is unknown.</summary>
    public sealed record Validation(IReadOnlyList<ValidationError> Errors) : ReserveSpaceError;

    /// <summary>The request is well formed but breaks reservation rules.</summary>
    public sealed record RulesViolated(IReadOnlyList<ReservationRuleViolation> Violations) : ReserveSpaceError;

    public IReadOnlyList<string> ToMessages() => this switch
    {
        Validation e => [.. e.Errors.Select(error => error.ToMessage())],
        RulesViolated e => [.. e.Violations.Select(violation => violation.ToMessage())],
        _ => [GetType().Name],
    };
}
