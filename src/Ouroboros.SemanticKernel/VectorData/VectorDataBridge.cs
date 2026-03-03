// <copyright file="VectorDataBridge.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using LangChain.Databases;
using LangChain.DocumentLoaders;
using Microsoft.Extensions.VectorData;
using Ouroboros.Domain.Vectors;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;
using OuroVectorStore = Ouroboros.Domain.Vectors.IVectorStore;

namespace Ouroboros.SemanticKernel.VectorData;

/// <summary>
/// Bidirectional adapter between Ouroboros' <see cref="IAdvancedVectorStore"/>
/// and Semantic Kernel's <see cref="SkVectorStore"/> (Microsoft.Extensions.VectorData).
/// <para>
/// Use <see cref="SkToOuroborosAdapter"/> to wrap an SK <see cref="SkVectorStore"/>
/// and expose it as an Ouroboros <see cref="IAdvancedVectorStore"/>.
/// </para>
/// <para>
/// Use <see cref="OuroborosToSkAdapter"/> to wrap an Ouroboros <see cref="IAdvancedVectorStore"/>
/// and expose it as an SK <see cref="SkVectorStore"/>.
/// </para>
/// </summary>
public static class VectorDataBridge
{
    /// <summary>
    /// Wraps an SK <see cref="SkVectorStore"/> to expose it as an Ouroboros <see cref="IAdvancedVectorStore"/>.
    /// </summary>
    /// <param name="skStore">The SK vector store to wrap.</param>
    /// <param name="collectionName">The collection name to operate on.</param>
    /// <param name="vectorDimension">The vector dimension for the collection.</param>
    /// <returns>An Ouroboros <see cref="IAdvancedVectorStore"/> backed by the SK store.</returns>
    public static IAdvancedVectorStore ToOuroboros(
        SkVectorStore skStore,
        string collectionName,
        int vectorDimension = 1536)
    {
        ArgumentNullException.ThrowIfNull(skStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        return new SkToOuroborosAdapter(skStore, collectionName, vectorDimension);
    }

    /// <summary>
    /// Wraps an Ouroboros <see cref="IAdvancedVectorStore"/> to expose it as an SK <see cref="SkVectorStore"/>.
    /// </summary>
    /// <param name="ouroStore">The Ouroboros vector store to wrap.</param>
    /// <param name="collectionName">The logical collection name exposed to SK consumers.</param>
    /// <returns>An SK <see cref="SkVectorStore"/> backed by the Ouroboros store.</returns>
    public static SkVectorStore ToSk(
        IAdvancedVectorStore ouroStore,
        string collectionName)
    {
        ArgumentNullException.ThrowIfNull(ouroStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        return new OuroborosToSkAdapter(ouroStore, collectionName);
    }
}
