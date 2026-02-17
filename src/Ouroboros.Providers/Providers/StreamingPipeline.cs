using System.Reactive.Linq;

namespace Ouroboros.Providers;

/// <summary>
/// Streaming pipeline builder for Rx-based operations.
/// </summary>
public sealed class StreamingPipeline
{
    private readonly CollectiveMind _mind;
    private readonly List<Func<IObservable<(bool IsThinking, string Chunk)>, IObservable<(bool IsThinking, string Chunk)>>> _transformations = new();

    public StreamingPipeline(CollectiveMind mind)
    {
        _mind = mind;
    }

    /// <summary>
    /// Starts streaming from a prompt.
    /// </summary>
    public StreamingPipeline From(string prompt)
    {
        return this;
    }

    /// <summary>
    /// Filters thinking chunks only.
    /// </summary>
    public StreamingPipeline OnlyThinking()
    {
        _transformations.Add(stream => stream.Where(t => t.IsThinking));
        return this;
    }

    /// <summary>
    /// Filters content chunks only.
    /// </summary>
    public StreamingPipeline OnlyContent()
    {
        _transformations.Add(stream => stream.Where(t => !t.IsThinking));
        return this;
    }

    /// <summary>
    /// Transforms chunks.
    /// </summary>
    public StreamingPipeline Transform(Func<string, string> transform)
    {
        _transformations.Add(stream =>
            stream.Select(t => (t.IsThinking, transform(t.Chunk))));
        return this;
    }

    /// <summary>
    /// Buffers chunks by time.
    /// </summary>
    public StreamingPipeline Buffer(TimeSpan window)
    {
        _transformations.Add(stream =>
            stream.Buffer(window)
                .Where(b => b.Count > 0)
                .Select(b => (b.Last().IsThinking, string.Concat(b.Select(c => c.Chunk)))));
        return this;
    }

    /// <summary>
    /// Throttles the stream.
    /// </summary>
    public StreamingPipeline Throttle(TimeSpan interval)
    {
        _transformations.Add(stream => stream.Throttle(interval));
        return this;
    }

    /// <summary>
    /// Executes the pipeline and returns the observable.
    /// </summary>
    public IObservable<(bool IsThinking, string Chunk)> Execute(string prompt, CancellationToken ct = default)
    {
        IObservable<(bool IsThinking, string Chunk)> stream = _mind.StreamWithThinkingAsync(prompt, ct);

        foreach (var transform in _transformations)
        {
            stream = transform(stream);
        }

        return stream;
    }

    /// <summary>
    /// Executes and collects the final result.
    /// </summary>
    public async Task<ThinkingResponse> ExecuteAndCollectAsync(string prompt, CancellationToken ct = default)
    {
        var thinkingBuilder = new StringBuilder();
        var contentBuilder = new StringBuilder();

        await Execute(prompt, ct).ForEachAsync(chunk =>
        {
            if (chunk.IsThinking)
                thinkingBuilder.Append(chunk.Chunk);
            else
                contentBuilder.Append(chunk.Chunk);
        }, ct);

        return new ThinkingResponse(
            thinkingBuilder.Length > 0 ? thinkingBuilder.ToString() : null,
            contentBuilder.ToString());
    }
}