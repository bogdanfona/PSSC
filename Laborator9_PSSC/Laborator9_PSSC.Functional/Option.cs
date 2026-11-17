using System.Diagnostics;

namespace Laborator9_PSSC.Functional;

/// <summary>A value that may legitimately be missing: <see cref="Some"/> or <see cref="None"/>. Unlike <c>T?</c>, both cases must be handled.</summary>
public abstract record Option<T>
    where T : notnull
{
    private Option() { }

    public sealed record Some(T Value) : Option<T>;

    public sealed record None : Option<T>;

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) => this switch
    {
        Some present => some(present.Value),
        None => none(),
        _ => throw new UnreachableException(),
    };
}

public static class Option
{
    public static Option<T> Some<T>(T value) where T : notnull => new Option<T>.Some(value);

    public static Option<T> None<T>() where T : notnull => new Option<T>.None();
}
