using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Extension methods to convert <see cref="IObservable{T}"/> to <see cref="IAsyncEnumerable{T}"/>
/// using <see cref="Channel{T}"/> for backpressure-safe bridging.
/// </summary>
public static class ObservableAsyncEnumerableExtensions
{
    /// <summary>
    /// Converts an <see cref="IObservable{T}"/> to an <see cref="IAsyncEnumerable{T}"/>
    /// using an unbounded channel as the bridge.
    /// </summary>
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this IObservable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        IDisposable subscription = source.Subscribe(
            onNext: item => channel.Writer.TryWrite(item),
            onError: ex => channel.Writer.TryComplete(ex),
            onCompleted: () => channel.Writer.TryComplete());

        try
        {
            await foreach (T item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            subscription.Dispose();
        }
    }
}
