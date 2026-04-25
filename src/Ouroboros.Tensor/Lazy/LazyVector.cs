// <copyright file="LazyVector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Lazy;

/// <summary>
/// Defers the materialisation of a vector's float data until explicitly requested via
/// <see cref="GetAsync"/>. After the first successful fetch the result is cached; subsequent
/// calls return the cached value without additional network round-trips (R07).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="VectorHandle"/> stored inside this class represents <em>location</em>,
/// not data. No heap allocation for the vector occurs until <see cref="GetAsync"/> is first awaited.
/// </para>
/// <para>
/// Thread-safe: concurrent calls to <see cref="GetAsync"/> are serialised by a semaphore so the
/// fetch executes exactly once (R19).
/// </para>
/// <para>
/// Implements <see cref="IAsyncDisposable"/> to allow explicit cache clearance (e.g. in memory-
/// constrained scenarios); disposal does not affect the remote store.
/// </para>
/// </remarks>
public sealed class LazyVector : IAsyncDisposable
{
    private readonly IHandleAwareVectorStore _store;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private float[]? _cached;
    private bool _loaded;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyVector"/> class.
    /// Initializes a new <see cref="LazyVector"/> referencing the given handle in the given store.
    /// </summary>
    public LazyVector(VectorHandle handle, IHandleAwareVectorStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Handle = handle;
        _store = store;
    }

    /// <summary>Gets the handle that identifies this vector in the remote store.</summary>
    public VectorHandle Handle { get; }

    /// <summary>
    /// Gets a value indicating whether gets whether the vector data has been loaded and cached locally.
    /// </summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Returns the vector data, fetching from the store on the first call and caching the result
    /// for subsequent calls (R07).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The float array on success, or a failure result with an error message.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public async Task<Result<float[], string>> GetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path: already loaded
        if (_loaded)
        {
            return Result<float[], string>.Success(_cached!);
        }

        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock
            if (_loaded)
            {
                return Result<float[], string>.Success(_cached!);
            }

            var result = await _store.FetchAsync(Handle, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _cached = result.Value;
                _loaded = true;
            }

            return result;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Clears the cached data and releases the semaphore. The vector can be re-fetched after
    /// disposal if the instance is reused (though this is unusual).
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cached = null;
            _loaded = false;
            _fetchLock.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
