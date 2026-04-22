// <copyright file="VectorStoreDocumentRecord.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.DocumentLoaders;
using Ouroboros.Domain.Vectors;
using Microsoft.Extensions.VectorData;

namespace Ouroboros.SemanticKernel.VectorData;

/// <summary>
/// SK-compatible record model that maps to/from domain <see cref="Document"/>
/// and <see cref="Vector"/> types used by Ouroboros' vector store interfaces.
/// </summary>
internal sealed class VectorStoreDocumentRecord
{
    /// <summary>
    /// Gets or sets the unique record identifier.
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document text content.
    /// </summary>
    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the embedding vector.
    /// </summary>
    // Note: Runtime dimension from BuildDefinition() takes precedence over this attribute value
    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    /// <summary>
    /// Converts an Ouroboros <see cref="Vector"/> into a <see cref="VectorStoreDocumentRecord"/>.
    /// </summary>
    internal static VectorStoreDocumentRecord FromVector(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        string id = vector.Id ?? Guid.NewGuid().ToString();
        string content = vector.Text ?? string.Empty;

        return new VectorStoreDocumentRecord
        {
            Id = id,
            Content = content,
            Embedding = vector.Embedding ?? ReadOnlyMemory<float>.Empty,
        };
    }

    /// <summary>
    /// Converts this record back to an Ouroboros <see cref="Document"/>.
    /// </summary>
    internal Document ToDocument()
    {
        return new Document(Content, new Dictionary<string, object>
        {
            ["id"] = Id,
        });
    }

    /// <summary>
    /// Builds a <see cref="VectorStoreCollectionDefinition"/> for this record type
    /// with the specified vector dimension.
    /// </summary>
    internal static VectorStoreCollectionDefinition BuildDefinition(int vectorDimension)
    {
        return new VectorStoreCollectionDefinition
        {
            Properties = new List<VectorStoreProperty>
            {
                new VectorStoreKeyProperty(nameof(Id), typeof(string)),
                new VectorStoreDataProperty(nameof(Content), typeof(string)),
                new VectorStoreVectorProperty(nameof(Embedding), typeof(ReadOnlyMemory<float>), vectorDimension),
            },
        };
    }
}
