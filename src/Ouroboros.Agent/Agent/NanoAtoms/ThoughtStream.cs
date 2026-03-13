// <copyright file="ThoughtStream.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Threading.Channels;
using Ouroboros.Core.Monads;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// A channel-based async stream that connects ThoughtFragments to NanoOuroborosAtoms.
/// Each stream processes fragments through its bound atom, producing DigestFragments.
/// Uses System.Threading.Channels for backpressure-aware async producer/consumer pattern.
/// </summary>
public sealed class ThoughtStream : IAsyncDisposable
{
    private readonly Channel<ThoughtFragment> _inputChannel;
    private readonly Channel<DigestFragment> _outputChannel;
    private readonly NanoOuroborosAtom _atom;
    private readonly CancellationTokenSource _cts = new();
    private Task? _processingTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThoughtStream"/> class.
    /// </summary>
    /// <param name="atom">The NanoOuroborosAtom that processes fragments in this stream.</param>
    /// <param name="bufferSize">Channel buffer size for backpressure (default 16).</param>
    public ThoughtStream(NanoOuroborosAtom atom, int bufferSize = 16)
    {
        ArgumentNullException.ThrowIfNull(atom);
        _atom = atom;

        var options = new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        };

        _inputChannel = Channel.CreateBounded<ThoughtFragment>(options);
        _outputChannel = Channel.CreateBounded<DigestFragment>(options);
    }

    /// <summary>Gets the unique identifier of the bound atom.</summary>
    public Guid AtomId => _atom.AtomId;

    /// <summary>Gets whether the stream is actively processing.</summary>
    public bool IsProcessing => _processingTask != null && !_processingTask.IsCompleted;

    /// <summary>Gets the number of digests produced so far.</summary>
    public int DigestsProduced { get; private set; }

    /// <summary>
    /// Starts the stream's processing loop. The atom will read from the input channel,
    /// process each fragment, and write digests to the output channel.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_processingTask != null)
        {
            return;
        }

        _processingTask = ProcessLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Writes a ThoughtFragment to the stream's input channel.
    /// </summary>
    /// <param name="fragment">The fragment to process.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask WriteAsync(ThoughtFragment fragment, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _inputChannel.Writer.WriteAsync(fragment, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Signals that no more fragments will be written. The stream will complete
    /// after processing all remaining fragments.
    /// </summary>
    public void Complete()
    {
        _inputChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Reads all DigestFragments produced by this stream.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of DigestFragments.</returns>
    public IAsyncEnumerable<DigestFragment> ReadAllAsync(CancellationToken ct = default)
    {
        return _outputChannel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Waits for the stream to finish processing all input fragments.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task WaitForCompletionAsync(CancellationToken ct = default)
    {
        if (_processingTask != null)
        {
            await _processingTask.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Collects all digest fragments from this stream into a list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All digest fragments produced by this stream.</returns>
    public async Task<List<DigestFragment>> CollectDigestsAsync(CancellationToken ct = default)
    {
        List<DigestFragment> digests = [];
await foreach (DigestFragment digest in ReadAllAsync(ct).ConfigureAwait(false))
        {
            digests.Add(digest);
        }

        return digests;
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        try
        {
await foreach (ThoughtFragment fragment in _inputChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                Result<DigestFragment, string> result = await _atom.ProcessAsync(fragment, ct).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    await _outputChannel.Writer.WriteAsync(result.Value, ct).ConfigureAwait(false);
                    DigestsProduced++;
                }

                // On failure, we skip the fragment (circuit breaker may be open)
                // The consolidator will work with whatever digests it gets
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        finally
        {
            _outputChannel.Writer.TryComplete();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _cts.CancelAsync().ConfigureAwait(false);
        _inputChannel.Writer.TryComplete();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _atom.Dispose();
        _cts.Dispose();
    }
}
