// <copyright file="FileWalPersistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Ouroboros.Network.Json;

namespace Ouroboros.Network.Persistence;

/// <summary>
/// File-based implementation of the Write-Ahead Log for Merkle-DAG persistence.
/// Uses newline-delimited JSON (NDJSON) format for append-only durability.
/// Thread-safe via internal locking.
/// </summary>
public sealed class FileWalPersistence : IGraphPersistence
{
    private readonly string _walFilePath;
    private readonly SemaphoreSlim _writeLock;
    private readonly StreamWriter? _writer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWalPersistence"/> class.
    /// </summary>
    /// <param name="walFilePath">The path to the WAL file.</param>
    public FileWalPersistence(string walFilePath)
    {
        ArgumentNullException.ThrowIfNull(walFilePath);
        _walFilePath = walFilePath;
        _writeLock = new SemaphoreSlim(1, 1);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(walFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open file in append mode
        _writer = new StreamWriter(
            new FileStream(walFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
            System.Text.Encoding.UTF8)
        {
            AutoFlush = false, // Batch writes for performance
        };
    }

    /// <summary>
    /// Appends a node addition to the Write-Ahead Log.
    /// </summary>
    /// <param name="node">The node to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AppendNodeAsync(MonadNode node, CancellationToken ct = default)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var nodeJson = JsonSerializer.Serialize(node, JsonDefaults.Default);

        var entry = new WalEntry(WalEntryType.AddNode, DateTimeOffset.UtcNow, nodeJson);
        await AppendEntryAsync(entry, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Appends an edge addition to the Write-Ahead Log.
    /// </summary>
    /// <param name="edge">The edge to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AppendEdgeAsync(TransitionEdge edge, CancellationToken ct = default)
    {
        if (edge == null)
        {
            throw new ArgumentNullException(nameof(edge));
        }

        var edgeJson = JsonSerializer.Serialize(edge, JsonDefaults.Default);

        var entry = new WalEntry(WalEntryType.AddEdge, DateTimeOffset.UtcNow, edgeJson);
        await AppendEntryAsync(entry, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes all pending writes to durable storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_writer != null)
            {
                await _writer.FlushAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Replays all entries from the Write-Ahead Log in chronological order.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of WAL entries.</returns>
    public async IAsyncEnumerable<WalEntry> ReplayAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!File.Exists(_walFilePath))
        {
            yield break; // Empty WAL - nothing to replay
        }

        using var reader = new StreamReader(
            new FileStream(_walFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
            System.Text.Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // Skip empty lines
            }

            WalEntry? entry = null;
            try
            {
                entry = JsonSerializer.Deserialize<WalEntry>(line);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[WAL] Skipping corrupted entry: {ex.Message}");
                continue;
            }

            if (entry != null)
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Disposes the persistence layer and closes the WAL file.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writer != null)
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
            }

            _disposed = true;
        }
        finally
        {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }

    private async Task AppendEntryAsync(WalEntry entry, CancellationToken ct)
    {
        ThrowIfDisposed();

        var entryJson = JsonSerializer.Serialize(entry, JsonDefaults.Default);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_writer != null)
            {
                await _writer.WriteLineAsync(entryJson.AsMemory(), ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileWalPersistence));
        }
    }

    #region Explicit IGraphPersistence implementation (Abstractions.Network types)

    // The IGraphPersistence interface is bound to Ouroboros.Abstractions.Network types,
    // while this class operates on the richer Ouroboros.Network types.
    // These explicit implementations satisfy the interface contract.

    /// <inheritdoc />
    async Task IGraphPersistence.AppendNodeAsync(
        Ouroboros.Abstractions.Network.MonadNode node,
        CancellationToken ct)
    {
        // Convert from Abstractions.Network.MonadNode to Network.MonadNode
        // Note: The Network.MonadNode constructor will compute the hash automatically
        var networkNode = new MonadNode(
            node.Id,
            node.TypeName,
            node.PayloadJson,
            node.CreatedAt,
            node.ParentIds);

        await AppendNodeAsync(networkNode, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    async Task IGraphPersistence.AppendEdgeAsync(
        Ouroboros.Abstractions.Network.TransitionEdge edge,
        CancellationToken ct)
    {
        // Convert from Abstractions.Network.TransitionEdge to Network.TransitionEdge
        // The abstraction edge has SourceId/TargetId while Network has InputIds/OutputId
        // We need to serialize the metadata to JSON for the OperationSpecJson field
        var metadataJson = JsonSerializer.Serialize(edge.Metadata, JsonDefaults.Default);

        var networkEdge = new TransitionEdge(
            edge.Id,
            ImmutableArray.Create(edge.SourceId),
            edge.TargetId,
            edge.TransitionType,
            metadataJson,
            edge.CreatedAt);

        await AppendEdgeAsync(networkEdge, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    async IAsyncEnumerable<Ouroboros.Abstractions.Network.WalEntry> IGraphPersistence.ReplayAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Convert from Network.Persistence.WalEntry to Abstractions.Network.WalEntry
        long sequenceNumber = 0;
        await foreach (var entry in ReplayAsync(ct))
        {
            yield return new Ouroboros.Abstractions.Network.WalEntry(
                Guid.NewGuid(),
                entry.Type.ToString(),
                entry.Timestamp,
                entry.PayloadJson,
                sequenceNumber++);
        }
    }

    #endregion
}
