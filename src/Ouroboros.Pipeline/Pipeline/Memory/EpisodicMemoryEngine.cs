// <copyright file="EpisodicMemoryEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Memory;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Verification;
using Qdrant.Client;

/// <summary>
/// Implementation of episodic memory system using Qdrant for vector storage.
/// Provides semantic search, consolidation, and experience-based planning.
/// </summary>
public sealed partial class EpisodicMemoryEngine : IEpisodicMemoryEngine, IAsyncDisposable
{
    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly ILogger<EpisodicMemoryEngine>? _logger;
    private readonly string _collectionName;
    private readonly bool _disposeClient;
    private readonly SemaphoreSlim _collectionInitLock = new(1, 1);
    private volatile bool _collectionInitialized;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public EpisodicMemoryEngine(
        QdrantClient qdrantClient,
        IQdrantCollectionRegistry registry,
        IEmbeddingModel embeddingModel,
        ILogger<EpisodicMemoryEngine>? logger = null)
    {
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        ArgumentNullException.ThrowIfNull(registry);
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        _collectionName = registry.GetCollectionName(QdrantCollectionRole.EpisodicMemory);
        _logger = logger;
        _disposeClient = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicMemoryEngine"/> class.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <param name="logger">Optional logger instance.</param>
    public EpisodicMemoryEngine(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string collectionName = "episodic_memory",
        ILogger<EpisodicMemoryEngine>? logger = null)
    {
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger;
        _disposeClient = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicMemoryEngine"/> class with connection string.
    /// </summary>
    [Obsolete("Use the constructor accepting QdrantClient + IQdrantCollectionRegistry from DI.")]
    public EpisodicMemoryEngine(
        string qdrantConnectionString,
        IEmbeddingModel embeddingModel,
        string collectionName = "episodic_memory",
        ILogger<EpisodicMemoryEngine>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(qdrantConnectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(qdrantConnectionString));
        }

        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger;

        var uri = new Uri(qdrantConnectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6334;
        var useHttps = uri.Scheme == "https";

        _qdrantClient = new QdrantClient(host, port, useHttps);
        _disposeClient = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposeClient)
        {
            _qdrantClient?.Dispose();
        }

        _collectionInitLock.Dispose();

        await Task.CompletedTask;
    }
}
