namespace Laborator2_PSSC.Domain;

// Closed hierarchy: a result is either Success or Failure, nothing else.
public abstract record Result<T>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(string Error) : Result<T>;

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) => this switch
    {
        Success s => next(s.Value),
        Failure f => new Result<TOut>.Failure(f.Error),
        _ => throw new InvalidOperationException()
    };

    public Result<TOut> Map<TOut>(Func<T, TOut> map) => Bind(v => Result.Ok(map(v)));
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Result<T>.Success(value);
    public static Result<T> Fail<T>(string error) => new Result<T>.Failure(error);
}
