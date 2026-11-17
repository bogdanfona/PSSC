using System.Diagnostics;

namespace Laborator9_PSSC.Functional;

/// <summary>
/// The outcome of an operation that can fail: <see cref="Ok"/> with a value or <see cref="Error"/> with an error
/// (F# <c>Result</c>, chapter 10 of <i>Domain Modeling Made Functional</i>). Closed: the constructor is private.
/// </summary>
public abstract record Result<TSuccess, TFailure>
    where TSuccess : notnull
    where TFailure : notnull
{
    private Result() { }

    public sealed record Ok(TSuccess Value) : Result<TSuccess, TFailure>;

    public sealed record Error(TFailure Value) : Result<TSuccess, TFailure>;

    /// <summary>Folds both branches into one value; the compiler makes the caller handle success and failure.</summary>
    public TResult Match<TResult>(Func<TSuccess, TResult> ok, Func<TFailure, TResult> error) => this switch
    {
        Ok success => ok(success.Value),
        Error failure => error(failure.Value),
        _ => throw new UnreachableException(),
    };

    public static implicit operator Result<TSuccess, TFailure>(TSuccess value) => new Ok(value);

    public static implicit operator Result<TSuccess, TFailure>(TFailure error) => new Error(error);
}

/// <summary>Combinators without a receiver. <c>Combine</c> is applicative: it reports every error, unlike <c>Bind</c>.</summary>
public static class Result
{
    public static Result<TResult, IReadOnlyList<TFailure>> Combine<T1, T2, T3, T4, TFailure, TResult>(
        Result<T1, TFailure> first,
        Result<T2, TFailure> second,
        Result<T3, TFailure> third,
        Result<T4, TFailure> fourth,
        Func<T1, T2, T3, T4, TResult> project)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where TFailure : notnull
        where TResult : notnull
    {
        if (first is Result<T1, TFailure>.Ok ok1
            && second is Result<T2, TFailure>.Ok ok2
            && third is Result<T3, TFailure>.Ok ok3
            && fourth is Result<T4, TFailure>.Ok ok4)
        {
            return new Result<TResult, IReadOnlyList<TFailure>>.Ok(project(ok1.Value, ok2.Value, ok3.Value, ok4.Value));
        }

        List<TFailure> errors = [];
        CollectError(first, errors);
        CollectError(second, errors);
        CollectError(third, errors);
        CollectError(fourth, errors);
        return new Result<TResult, IReadOnlyList<TFailure>>.Error(errors);
    }

    /// <summary>Ok when no check failed, otherwise every failure, in order. Used for independent business rules.</summary>
    public static Result<TSuccess, IReadOnlyList<TFailure>> Check<TSuccess, TFailure>(TSuccess value, params IEnumerable<TFailure?> failures)
        where TSuccess : notnull
        where TFailure : class
    {
        List<TFailure> errors = [.. failures.OfType<TFailure>()];
        return errors.Count == 0
            ? new Result<TSuccess, IReadOnlyList<TFailure>>.Ok(value)
            : new Result<TSuccess, IReadOnlyList<TFailure>>.Error(errors);
    }

    private static void CollectError<T, TFailure>(Result<T, TFailure> result, List<TFailure> errors)
        where T : notnull
        where TFailure : notnull
    {
        if (result is Result<T, TFailure>.Error error)
            errors.Add(error.Value);
    }
}
