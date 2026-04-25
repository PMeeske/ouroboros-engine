using System.Runtime.CompilerServices;
using System.Threading.Channels;
using R3;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Extension methods to convert <see cref="Observable{T}"/> to <see cref="IAsyncEnumerable{T}"/>
/// using <see cref="Channel{T}"/> for backpressure-safe bridging.
/// </summary>
public static class ObservableAsyncEnumerableExtensions
{
    /// <summary>
    /// Converts an R3 <see cref="Observable{T}"/> to an <see cref="IAsyncEnumerable{T}"/>
    /// using an unbounded channel as the bridge.
    /// </summary>
    /// <returns></returns>
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this Observable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true,
        });

        IDisposable subscription = source.Subscribe(
            item => channel.Writer.TryWrite(item),
            result => channel.Writer.TryComplete(result.IsFailure ? result.Exception : null));

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
            channel.Writer.TryComplete();
        }
    }
}
