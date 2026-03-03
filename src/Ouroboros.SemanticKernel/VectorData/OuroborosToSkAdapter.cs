// <copyright file="OuroborosToSkAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.VectorData;
using Ouroboros.Domain.Vectors;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;

namespace Ouroboros.SemanticKernel.VectorData;

/// <summary>
/// Adapts an Ouroboros <see cref="IAdvancedVectorStore"/> to SK's <see cref="SkVectorStore"/>.
/// Exposes a single collection backed by the Ouroboros store.
/// </summary>
/// <remarks>
/// This is a minimal adapter. It exposes the Ouroboros store as a single-collection
/// SK vector store. Advanced collection management (create/delete/list) is mapped
/// to the underlying Ouroboros operations where possible.
/// </remarks>
internal sealed class OuroborosToSkAdapter : SkVectorStore
{
    private readonly IAdvancedVectorStore _ouroStore;
    private readonly string _collectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosToSkAdapter"/> class.
    /// </summary>
    internal OuroborosToSkAdapter(IAdvancedVectorStore ouroStore, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(ouroStore);
        _ouroStore = ouroStore;
        _collectionName = collectionName;
    }

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        // The adapter does not support arbitrary typed collection access.
        // This is a bridge -- callers needing full SK collection features should
        // use the native SK Qdrant connector directly.
        throw new NotSupportedException(
            $"The Ouroboros-to-SK bridge does not support typed collection access. " +
            $"Use the native SK Qdrant connector for full collection support with type '{typeof(TRecord).Name}'.");
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition)
    {
        throw new NotSupportedException(
            "The Ouroboros-to-SK bridge does not support dynamic collection access. " +
            "Use the native SK Qdrant connector for full collection support.");
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Ouroboros operates on a single collection; return its name if the store is accessible.
        VectorStoreInfo info;

        try
        {
            info = await _ouroStore.GetInfoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(info.Name))
        {
            yield return info.Name;
        }
        else
        {
            yield return _collectionName;
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _ouroStore.GetInfoAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(info.Status, "ready", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(info.Status, "green", StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        await _ouroStore.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IAdvancedVectorStore))
        {
            return _ouroStore;
        }

        if (serviceType.IsInstanceOfType(this))
        {
            return this;
        }

        return null;
    }
}
