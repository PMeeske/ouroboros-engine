using R3;

namespace Ouroboros.Providers;

/// <summary>
/// DSL operation that can be composed into pipelines.
/// Monad for chaining collective mind operations with Rx streaming.
/// </summary>
public sealed class MindOperation<T>
{
    private readonly Func<CollectiveMind, CancellationToken, Task<T>> _execute;
    private readonly Func<CollectiveMind, CancellationToken, Observable<(bool IsThinking, string Chunk)>>? _stream;

    private MindOperation(
        Func<CollectiveMind, CancellationToken, Task<T>> execute,
        Func<CollectiveMind, CancellationToken, Observable<(bool IsThinking, string Chunk)>>? stream = null)
    {
        _execute = execute;
        _stream = stream;
    }

    /// <summary>Creates a pure value operation.</summary>
    /// <returns></returns>
    public static MindOperation<T> Return(T value) =>
        new((_, _) => Task.FromResult(value));

    /// <summary>Creates an async operation.</summary>
    /// <returns></returns>
    public static MindOperation<T> FromAsync(Func<CollectiveMind, CancellationToken, Task<T>> execute) =>
        new(execute);

    /// <summary>Creates a streaming operation.</summary>
    /// <returns></returns>
    public static MindOperation<T> FromStream(
        Func<CollectiveMind, CancellationToken, Observable<(bool IsThinking, string Chunk)>> stream,
        Func<CollectiveMind, CancellationToken, Task<T>> finalResult) =>
        new(finalResult, stream);

    /// <summary>Executes the operation against a collective mind.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<T> ExecuteAsync(CollectiveMind mind, CancellationToken ct = default) =>
        _execute(mind, ct);

    /// <summary>Gets the streaming observable if available.</summary>
    /// <returns></returns>
    public Observable<(bool IsThinking, string Chunk)>? GetStream(CollectiveMind mind, CancellationToken ct = default) =>
        _stream?.Invoke(mind, ct);

    /// <summary>Gets a value indicating whether whether this operation supports streaming.</summary>
    public bool SupportsStreaming => _stream != null;

    // Functor: map
    public MindOperation<TResult> Select<TResult>(Func<T, TResult> selector) =>
        new(async (mind, ct) => selector(await _execute(mind, ct).ConfigureAwait(false)), _stream);

    // Monad: flatMap
    public MindOperation<TResult> SelectMany<TResult>(Func<T, MindOperation<TResult>> selector) =>
        new(async (mind, ct) =>
        {
            T result = await _execute(mind, ct).ConfigureAwait(false);
            return await selector(result).ExecuteAsync(mind, ct).ConfigureAwait(false);
        });

    // LINQ support
    public MindOperation<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, MindOperation<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector) =>
        new(async (mind, ct) =>
        {
            T first = await _execute(mind, ct).ConfigureAwait(false);
            TIntermediate second = await selector(first).ExecuteAsync(mind, ct).ConfigureAwait(false);
            return resultSelector(first, second);
        });

    // Combine with another operation
    public MindOperation<(T, TOther)> Zip<TOther>(MindOperation<TOther> other) =>
        new(async (mind, ct) =>
        {
            var task1 = _execute(mind, ct);
            var task2 = other.ExecuteAsync(mind, ct);
            await Task.WhenAll(task1, task2).ConfigureAwait(false);
            return (task1.Result, task2.Result);
        });
}
