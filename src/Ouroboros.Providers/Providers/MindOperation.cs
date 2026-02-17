namespace Ouroboros.Providers;

/// <summary>
/// DSL operation that can be composed into pipelines.
/// Monad for chaining collective mind operations with Rx streaming.
/// </summary>
public sealed class MindOperation<T>
{
    private readonly Func<CollectiveMind, CancellationToken, Task<T>> _execute;
    private readonly Func<CollectiveMind, CancellationToken, IObservable<(bool IsThinking, string Chunk)>>? _stream;

    private MindOperation(
        Func<CollectiveMind, CancellationToken, Task<T>> execute,
        Func<CollectiveMind, CancellationToken, IObservable<(bool IsThinking, string Chunk)>>? stream = null)
    {
        _execute = execute;
        _stream = stream;
    }

    /// <summary>Creates a pure value operation.</summary>
    public static MindOperation<T> Return(T value) =>
        new((_, _) => Task.FromResult(value));

    /// <summary>Creates an async operation.</summary>
    public static MindOperation<T> FromAsync(Func<CollectiveMind, CancellationToken, Task<T>> execute) =>
        new(execute);

    /// <summary>Creates a streaming operation.</summary>
    public static MindOperation<T> FromStream(
        Func<CollectiveMind, CancellationToken, IObservable<(bool IsThinking, string Chunk)>> stream,
        Func<CollectiveMind, CancellationToken, Task<T>> finalResult) =>
        new(finalResult, stream);

    /// <summary>Executes the operation against a collective mind.</summary>
    public Task<T> ExecuteAsync(CollectiveMind mind, CancellationToken ct = default) =>
        _execute(mind, ct);

    /// <summary>Gets the streaming observable if available.</summary>
    public IObservable<(bool IsThinking, string Chunk)>? GetStream(CollectiveMind mind, CancellationToken ct = default) =>
        _stream?.Invoke(mind, ct);

    /// <summary>Whether this operation supports streaming.</summary>
    public bool SupportsStreaming => _stream != null;

    // Functor: map
    public MindOperation<TResult> Select<TResult>(Func<T, TResult> selector) =>
        new(async (mind, ct) => selector(await _execute(mind, ct)), _stream);

    // Monad: flatMap
    public MindOperation<TResult> SelectMany<TResult>(Func<T, MindOperation<TResult>> selector) =>
        new(async (mind, ct) =>
        {
            T result = await _execute(mind, ct);
            return await selector(result).ExecuteAsync(mind, ct);
        });

    // LINQ support
    public MindOperation<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, MindOperation<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector) =>
        new(async (mind, ct) =>
        {
            T first = await _execute(mind, ct);
            TIntermediate second = await selector(first).ExecuteAsync(mind, ct);
            return resultSelector(first, second);
        });

    // Combine with another operation
    public MindOperation<(T, TOther)> Zip<TOther>(MindOperation<TOther> other) =>
        new(async (mind, ct) =>
        {
            var task1 = _execute(mind, ct);
            var task2 = other.ExecuteAsync(mind, ct);
            await Task.WhenAll(task1, task2);
            return (task1.Result, task2.Result);
        });
}