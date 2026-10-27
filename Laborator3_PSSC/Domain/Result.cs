namespace Laborator3_PSSC.Domain;

// Closed hierarchy: either Success with a value or Failure with an error.
public abstract record Result<T, TError>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T, TError>;
    public sealed record Failure(TError Error) : Result<T, TError>;

    // Runs the next step only on the success branch; a failure short-circuits the chain.
    public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> next) => this switch
    {
        Success s => next(s.Value),
        Failure f => new Result<TOut, TError>.Failure(f.Error),
        _ => throw new InvalidOperationException()
    };

    public Result<TOut, TError> Map<TOut>(Func<T, TOut> map) =>
        Bind(value => new Result<TOut, TError>.Success(map(value)));
}
