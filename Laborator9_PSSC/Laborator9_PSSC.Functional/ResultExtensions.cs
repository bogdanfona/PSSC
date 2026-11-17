namespace Laborator9_PSSC.Functional;

/// <summary>Railway-oriented combinators as C# 14 extension members: the type stays plain data, the functions live in a module.</summary>
public static class ResultExtensions
{
    extension<TSuccess, TFailure>(Result<TSuccess, TFailure> result)
        where TSuccess : notnull
        where TFailure : notnull
    {
        public bool IsOk => result is Result<TSuccess, TFailure>.Ok;

        public Result<TOut, TFailure> Map<TOut>(Func<TSuccess, TOut> map)
            where TOut : notnull
            => result.Match<Result<TOut, TFailure>>(
                value => new Result<TOut, TFailure>.Ok(map(value)),
                error => new Result<TOut, TFailure>.Error(error));

        /// <summary>Chains a step that can fail; the first error stops the chain.</summary>
        public Result<TOut, TFailure> Bind<TOut>(Func<TSuccess, Result<TOut, TFailure>> bind)
            where TOut : notnull
            => result.Match(bind, error => new Result<TOut, TFailure>.Error(error));

        public Result<TSuccess, TOut> MapError<TOut>(Func<TFailure, TOut> map)
            where TOut : notnull
            => result.Match<Result<TSuccess, TOut>>(
                value => new Result<TSuccess, TOut>.Ok(value),
                error => new Result<TSuccess, TOut>.Error(map(error)));

        /// <summary>An extra condition on the success branch (for example an existence check), turned into an error if false.</summary>
        public Result<TSuccess, TFailure> Ensure(Func<TSuccess, bool> predicate, Func<TSuccess, TFailure> error)
            => result.Bind(value => predicate(value) ? result : new Result<TSuccess, TFailure>.Error(error(value)));

        /// <summary>Runs an asynchronous effect (persistence) on the success branch; the value passes through unchanged.</summary>
        public async Task<Result<TSuccess, TFailure>> TapAsync(Func<TSuccess, Task> effect)
        {
            if (result is Result<TSuccess, TFailure>.Ok ok)
                await effect(ok.Value);
            return result;
        }
    }

    extension<TSuccess, TFailure>(Task<Result<TSuccess, TFailure>> resultTask)
        where TSuccess : notnull
        where TFailure : notnull
    {
        public async Task<Result<TOut, TFailure>> Map<TOut>(Func<TSuccess, TOut> map)
            where TOut : notnull
            => (await resultTask).Map(map);
    }
}
